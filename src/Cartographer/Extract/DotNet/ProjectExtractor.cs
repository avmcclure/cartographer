using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Cartographer.DotNetFiles;

namespace Cartographer;

/// <summary>
/// Order 10: one component per .NET project file (.csproj, .vbproj, .fsproj), SDK-style or legacy.
/// The file is read as XML. MSBuild is never run, so a value set through an import or a $(Property) is not seen,
/// except properties in the nearest Directory.Build.props and versions in the nearest Directory.Packages.props.
/// </summary>
public sealed class ProjectExtractor : IExtractor
{
    static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase) { [".csproj"] = "csharp", [".vbproj"] = "vbnet", [".fsproj"] = "fsharp" };
    static readonly string[] ViewExtensions = [".cshtml", ".razor", ".aspx"];
    const string WebApplicationGuid = "349c5851-65df-11da-9384-00065b846f21";
    static readonly Regex TestPackage = new(@"^(xunit|nunit|mstest|Microsoft\.NET\.Test\.Sdk|Microsoft\.VisualStudio\.(QualityTools\.UnitTestFramework|TestPlatform))", RegexOptions.IgnoreCase);
    static readonly Regex FrameworkAssembly = new(@"^(System.*|Microsoft\.CSharp|mscorlib|WindowsBase|Presentation.*|netstandard)$", RegexOptions.IgnoreCase);

    public int Order => 10;

    /// <summary>One project file as read. Dir is its directory in the checkout; BuildProps is the nearest Directory.Build.props, if any.</summary>
    sealed record Project(string Path, string Dir, XElement Root, XElement? BuildProps)
    {
        public string FileName => System.IO.Path.GetFileNameWithoutExtension(Path);
        public string? Sdk => Attr(Root, "Sdk") ?? Root.Children("Sdk").Select(e => Attr(e, "Name")).FirstOrDefault() ?? Root.Children("Import").Select(e => Attr(e, "Sdk")).FirstOrDefault(s => s is not null);
        public string AssemblyName => Prop("AssemblyName") ?? FileName;

        /// <summary>The first literal value of a property, from the project and then Directory.Build.props. Conditions are not evaluated.</summary>
        public string? Prop(string name) => new[] { Root, BuildProps }
            .SelectMany(r => r.Children("PropertyGroup").Elements().Named(name))
            .Select(e => e.Value.Trim()).FirstOrDefault(v => v.Length > 0 && !v.Contains("$("));

        /// <summary>The line a property is set on in the project file, or the line of the root element when it is not set there.</summary>
        public int LineOf(string name) => Line(Root.Children("PropertyGroup").Elements().Named(name).FirstOrDefault() ?? Root);

        /// <summary>Items of one type from every ItemGroup, conditional or not.</summary>
        public IEnumerable<XElement> Items(string name) => Root.Descendants().Named("ItemGroup").Elements().Named(name);

        public List<string> TargetFrameworks =>
            (Prop("TargetFrameworks") ?? Prop("TargetFramework"))?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            ?? (Prop("TargetFrameworkVersion") is { } v ? ["net" + v.TrimStart('v', 'V').Replace(".", "")] : []);
    }

    public void Extract(Extraction x)
    {
        var repo = x.Repo;
        var files = Index(repo);
        string? Find(string dir, string name) => files.GetValueOrDefault(Join(dir, name));
        var paths = repo.WithExtension(Languages.Keys.ToArray()).ToList();
        if (paths.Count == 0) return;

        // Each file belongs to the nearest project directory above it.
        var projectDirs = paths.Select(Dir).ToHashSet();
        var filesByProject = repo.Files.ToLookup(f => Up(Dir(f)).FirstOrDefault(projectDirs.Contains) ?? "\0");

        // First pass: a component per project, so project references can point at them in the second pass.
        var components = new Dictionary<string, (Project Project, Entry Component)>(StringComparer.OrdinalIgnoreCase);
        var testProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unreadable = 0;
        foreach (var path in paths)
        {
            if (Load(repo, path) is not { } root) { unreadable++; continue; }
            var dir = Dir(path);
            var buildProps = Up(dir).Select(d => Find(d, "Directory.Build.props")).FirstOrDefault(f => f is not null);
            var p = new Project(path, dir, root, buildProps is null ? null : Load(repo, buildProps));
            var packagesConfig = Find(dir, "packages.config") is { } pc ? Load(repo, pc) : null;

            var referenced = p.Items("PackageReference").Concat(p.Items("Reference")).Select(r => Attr(r, "Include"))
                .Concat(packagesConfig.Children("package").Select(e => Attr(e, "id")));
            if (string.Equals(p.Prop("IsTestProject"), "true", StringComparison.OrdinalIgnoreCase) || p.Sdk?.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase) == true
                || referenced.Any(n => n is not null && TestPackage.IsMatch(n)))
            {
                testProjects.Add(path);
                continue;
            }

            var own = filesByProject[dir].ToList();
            var c = x.Component(dir, p.AssemblyName, TypeOf(repo, p, own, Find));
            components[path] = (p, c);
            var frameworks = p.TargetFrameworks;
            c.Stack = [Languages[Path.GetExtension(path)], .. frameworks];

            Provide(c, p.AssemblyName, "assembly", x.Cite(c, "proj", path, p.LineOf("AssemblyName")));
            Provide(c, p.Prop("RootNamespace") ?? p.FileName.Replace(' ', '_'), "namespace", x.Cite(c, "proj", path, p.LineOf("RootNamespace")));
            ProvidePackage(x, c, p, own);

            var feeds = Up(dir).Select(d => Find(d, "nuget.config")).Where(f => f is not null)
                .SelectMany(f => Load(repo, f!).Children("packageSources").Elements().Named("add"))
                .Select(a => (Key: Attr(a, "key"), Url: Attr(a, "value"))).Where(f => f.Key is not null && f.Url is not null)
                .Select(f => $"{Tick(f.Key)} {SafeUrl(f.Url!)}").Distinct().Order(StringComparer.Ordinal).ToList();
            var binaries = p.Items("Reference").Select(HintPath).Where(h => h is not null && !InPackagesFolder(h)).Select(h => Tick(h!.Replace('\\', '/'))).Distinct().Order(StringComparer.Ordinal).ToList();
            c.Extracted =
                $"- Project: `{path}` ({(p.Sdk is null ? "legacy" : "SDK")} style{(p.Sdk is null ? "" : $", {p.Sdk}")}).\n" +
                $"- Output type: {p.Prop("OutputType") ?? (p.Sdk is { } sdk && (sdk.Contains("Web") || sdk.Contains("Worker")) ? "Exe (the SDK's default)" : "Library (default)")}.\n" +
                $"- Target frameworks: {(frameworks.Count > 0 ? string.Join(", ", frameworks) : "not set in the project file")}.\n" +
                (feeds.Count > 0 ? $"- NuGet feeds from nuget.config: {string.Join(", ", feeds)}.\n" : "") +
                (binaries.Count > 0 ? $"- Assemblies referenced by file path, origin unknown: {string.Join(", ", binaries)}.\n" : "");
        }

        // Second pass: what each project depends on.
        foreach (var (p, c) in components.Values)
        {
            string At(XElement e) => x.Cite(c, "proj", p.Path, Line(e));
            void DependOn(string name, string? version, string source) =>
                Extraction.AddLink(c, new Link { Type = "depends-on", Name = name, Version = version is null || version.Contains("$(") ? null : version, Basis = Basis.Extracted, Source = source });

            var centralFile = Up(p.Dir).Select(d => Find(d, "Directory.Packages.props")).FirstOrDefault(f => f is not null);
            var central = (centralFile is null ? null : Load(repo, centralFile))?.Descendants().Named("PackageVersion").ToList() ?? [];
            foreach (var r in p.Items("PackageReference"))
            {
                if (Attr(r, "Include") is not { } name || name.Contains("$(")) continue;
                DependOn(name, Attr(r, "Version") ?? r.Children("Version").FirstOrDefault()?.Value.Trim() ?? Attr(r, "VersionOverride")
                    ?? central.Where(v => string.Equals(Attr(v, "Include"), name, StringComparison.OrdinalIgnoreCase)).Select(v => Attr(v, "Version")).FirstOrDefault(), At(r));
            }

            var packagesConfig = Find(p.Dir, "packages.config");
            foreach (var e in (packagesConfig is null ? null : Load(repo, packagesConfig)).Children("package"))
                if (Attr(e, "id") is { } id) DependOn(id, Attr(e, "version"), x.Cite(c, "packages", packagesConfig!, Line(e)));

            foreach (var r in p.Items("Reference"))
            {
                if (Attr(r, "Include") is not { } include) continue;
                var name = include.Split(',')[0].Trim();
                var hint = HintPath(r);
                if (hint is null && FrameworkAssembly.IsMatch(name)) continue;
                // An assembly restored into the packages folder is already listed, under its package's name, in packages.config.
                if (hint is not null && packagesConfig is not null && InPackagesFolder(hint)) continue;
                var version = Regex.Match(include, @"Version=([\d.]+)");
                DependOn(name, version.Success ? version.Groups[1].Value : null, At(r));
            }

            foreach (var r in p.Items("COMReference"))
            {
                if (r.Children("Guid").FirstOrDefault()?.Value.Trim() is not { Length: > 0 } guid) continue;
                var major = r.Children("VersionMajor").FirstOrDefault()?.Value.Trim();
                var minor = r.Children("VersionMinor").FirstOrDefault()?.Value.Trim();
                DependOn("{" + guid.Trim('{', '}').ToUpperInvariant() + "}", major is null ? null : $"{major}.{minor ?? "0"}", At(r));
            }

            foreach (var r in p.Items("ProjectReference"))
            {
                if (Attr(r, "Include") is not { } include) continue;
                var target = Resolve(p.Dir, include);
                if (target is not null && testProjects.Contains(target)) continue;
                if (target is not null && components.TryGetValue(target, out var found))
                {
                    if (found.Component != c) Extraction.AddLink(c, new Link { Type = "depends-on", To = found.Component.Id, Basis = Basis.Extracted, Source = At(r) });
                }
                else DependOn(Path.GetFileNameWithoutExtension(include.Replace('\\', '/')), null, At(r));
            }
        }

        x.Repository.Extracted += $"- .NET projects: {components.Count}. Test projects skipped: {testProjects.Count}.{(unreadable > 0 ? $" Not readable as XML: {unreadable}." : "")}\n";
    }

    static string? HintPath(XElement reference) => reference.Children("HintPath").FirstOrDefault()?.Value.Trim() is { Length: > 0 } h ? h : Attr(reference, "HintPath");

    static bool InPackagesFolder(string? hintPath) => hintPath?.Replace('\\', '/').Split('/').Contains("packages", StringComparer.OrdinalIgnoreCase) == true;

    static void Provide(Entry c, string name, string kind, string source)
    {
        c.Provides ??= [];
        if (!c.Provides.Any(p => p.Name == name && p.As == kind)) c.Provides.Add(new Provided { Name = name, As = kind, Basis = Basis.Extracted, Source = source });
    }

    /// <summary>
    /// A package ID is provided only when the project says it is packed: IsPackable or GeneratePackageOnBuild is true, a PackageId is set,
    /// or a .nuspec sits beside it. MSBuild's own default (most SDK-style projects can be packed) proves nothing about what is published.
    /// </summary>
    static void ProvidePackage(Extraction x, Entry c, Project p, List<string> own)
    {
        bool True(string property) => string.Equals(p.Prop(property), "true", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(p.Prop("IsPackable"), "false", StringComparison.OrdinalIgnoreCase)) return;
        var nuspec = own.FirstOrDefault(f => Dir(f) == p.Dir && f.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (p.Prop("PackageId") is { } packageId) Provide(c, packageId, "nuget", x.Cite(c, "proj", p.Path, p.LineOf("PackageId")));
        else if (nuspec is not null)
        {
            var id = Load(x.Repo, nuspec).Children("metadata").Elements().Named("id").FirstOrDefault();
            var literal = id is not null && id.Value.Trim().Length > 0 && !id.Value.Contains('$');   // "$id$" is replaced with the assembly name when packing
            Provide(c, literal ? id!.Value.Trim() : p.AssemblyName, "nuget", x.Cite(c, "nuspec", nuspec, id is null ? null : Line(id)));
        }
        else if (True("IsPackable") || True("GeneratePackageOnBuild"))
            Provide(c, p.AssemblyName, "nuget", x.Cite(c, "proj", p.Path, p.LineOf(True("IsPackable") ? "IsPackable" : "GeneratePackageOnBuild")));
    }

    /// <summary>Component type from the SDK, output type, project type GUIDs and the files beside the project.</summary>
    static string TypeOf(Checkout repo, Project p, List<string> own, Func<string, string, string?> find)
    {
        var sdk = p.Sdk ?? "";
        var output = p.Prop("OutputType") ?? "Library";
        bool Is(string type) => output.Equals(type, StringComparison.OrdinalIgnoreCase);
        var web = sdk.Contains("Sdk.Web", StringComparison.OrdinalIgnoreCase) || sdk.Contains("BlazorWebAssembly", StringComparison.OrdinalIgnoreCase)
            || (p.Prop("ProjectTypeGuids") ?? "").Contains(WebApplicationGuid, StringComparison.OrdinalIgnoreCase);
        if (web) return own.Any(f => ViewExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) ? "site" : "service";
        if (sdk.Contains("Sdk.Worker", StringComparison.OrdinalIgnoreCase)) return "service";
        // A WCF host: .svc files, or an executable (console or Windows service) whose config declares services.
        if (own.Any(f => f.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))) return "service";
        if ((Is("Exe") || Is("WinExe")) && find(p.Dir, "app.config") is { } config
            && Load(repo, config).Children("system.serviceModel").Elements().Named("services").Elements().Named("service").Any()) return "service";
        return Is("WinExe") ? "desktop-app" : Is("Exe") ? "job" : "library";
    }
}
