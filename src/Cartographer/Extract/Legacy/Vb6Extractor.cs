using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>Order 20: one component per VB6 project file (.vbp), with the ProgIDs it provides and the libraries it references.</summary>
public sealed class Vb6Extractor : IExtractor
{
    // Libraries that ship with Windows, VB6 or MDAC: never another team's component.
    static readonly Regex Platform = new(@"^(stdole\d*|msvbvm\d+|vba\d*|vb6|vb5db|msado.*|msjro|msdasc|dao\d+|scrrun|msxml\d*|mscomct.*|comdlg32|comctl32|comct\d32|msflxgrd|mshflxgd|msdatgrd|msdatlst|msstdfmt|msbind|richtx32|tabctl32|msmask32|msinet|mswinsck|mscal|threed32)$", RegexOptions.IgnoreCase);
    static readonly Regex Guid = new(@"\{[0-9A-Fa-f-]{36}\}");
    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Exe"] = "Standard EXE", ["OleDll"] = "ActiveX DLL", ["OleExe"] = "ActiveX EXE", ["Control"] = "ActiveX control",
    };

    public int Order => 20;

    public void Extract(Extraction x)
    {
        var taken = new HashSet<string>();
        foreach (var vbp in x.Repo.WithExtension(".vbp"))
        {
            var keys = LegacyText.Read(x.Repo, vbp).Split('\n')
                .Select((text, i) => (Text: text.Trim(), Line: i + 1)).Where(l => l.Text.Contains('=') && !l.Text.StartsWith('['))
                .Select(l => (Key: l.Text[..l.Text.IndexOf('=')].Trim(), Value: l.Text[(l.Text.IndexOf('=') + 1)..].Trim(), l.Line)).ToList();
            (string Value, int Line)? First(string key) => keys.Where(k => k.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(k => ((string, int)?)(k.Value.Trim('"'), k.Line)).FirstOrDefault();
            List<(string Value, int Line)> All(string key) => keys.Where(k => k.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(k => (k.Value, k.Line)).ToList();

            var directory = LegacyText.Directory(vbp);
            var name = First("Name")?.Value is { Length: > 0 } n ? n : Path.GetFileNameWithoutExtension(vbp);
            var projectType = First("Type")?.Value ?? "Exe";
            var type = projectType.Equals("Exe", StringComparison.OrdinalIgnoreCase) ? "desktop-app" : "library";
            // The first project in a directory is the component rooted there; a second project beside it still gets its own entry.
            Entry c;
            if (taken.Add(directory) && !x.ComponentRoots.ContainsKey(directory)) c = x.Component(directory, name, type);
            else
            {
                c = x.Get(Ids.Component(x.Repo.Repository, name), Kinds.Component, name, () => $"{name}: a {type} in {x.Repo.Repository}; not yet described.");
                c.Type ??= type;
            }
            LegacyText.AddStack(c, "vb6");

            // Classes: a public, creatable class is reachable from outside as "<Project>.<Class>".
            var creatable = new List<string>();
            var others = new List<string>();
            foreach (var (value, _) in All("Class"))
            {
                var className = value.Split(';')[0].Trim();
                var path = LegacyText.Find(x.Repo, directory, value.Split(';')[^1]);
                if (path is null) { others.Add($"`{className}` (file not in the repository)"); continue; }
                var text = LegacyText.Read(x.Repo, path);
                className = Regex.Match(text, @"(?im)^Attribute\s+VB_Name\s*=\s*""([^""]+)""") is { Success: true } named ? named.Groups[1].Value : className;
                var exposed = Regex.Match(text, @"(?im)^Attribute\s+VB_Exposed\s*=\s*True\b");
                var notCreatable = Regex.IsMatch(text, @"(?im)^Attribute\s+VB_Creatable\s*=\s*False\b");
                if (!exposed.Success) others.Add($"`{className}` (private)");
                else if (notCreatable) others.Add($"`{className}` (public, not creatable)");
                else
                {
                    creatable.Add($"`{className}`");
                    Provide(c, $"{name}.{className}", "progid", x.Cite(c, LegacyText.Key(path), path, LegacyText.Line(text, exposed.Index)));
                }
            }
            if (First("ExeName32") is { Value.Length: > 0 } exe) Provide(c, exe.Value, "assembly", x.Cite(c, LegacyText.Key(vbp), vbp, exe.Line));

            // References and controls: in-house ones are links by file name, platform ones are only listed.
            var references = new List<string>();
            var platform = new List<string>();
            foreach (var (key, value, line) in keys.Where(k => k.Key.Equals("Reference", StringComparison.OrdinalIgnoreCase) || k.Key.Equals("Object", StringComparison.OrdinalIgnoreCase)))
            {
                // Reference=*\G{guid}#version#lcid#path#description   Object={guid}#version#lcid; file.ocx   Reference=*\Apath\project.vbp
                var parts = value.Split('#', 5);
                var file = key.Equals("Object", StringComparison.OrdinalIgnoreCase) && value.Contains(';') ? value[(value.LastIndexOf(';') + 1)..]
                    : parts.Length >= 4 ? parts[3] : Regex.Replace(value, @"^\*\\[A-Z]", "");
                file = file.Trim().Trim('"').Split('\\', '/')[^1];
                if (file.Length == 0) continue;
                var description = key.Equals("Reference", StringComparison.OrdinalIgnoreCase) && parts.Length == 5 ? parts[4].Trim() : "";
                var guid = Guid.Match(parts[0]).Value;
                if (Platform.IsMatch(Path.GetFileNameWithoutExtension(file)))
                {
                    platform.Add(description.Length > 0 ? $"`{file}` ({description})" : $"`{file}`");
                    continue;
                }
                var detail = string.Join("; ", new[] { description, guid.Length > 0 ? $"type library {guid}{(parts.Length >= 2 ? " version " + parts[1].Split(';')[0].Trim() : "")}" : "" }.Where(s => s.Length > 0));
                references.Add($"  - `{file}`{(detail.Length > 0 ? ": " + detail : "")}");
                LegacyText.Link(x, c, "depends-on", file, null, vbp, line);
            }

            var block = new StringBuilder();
            block.Append($"- VB6 project `{vbp}`: {(Types.TryGetValue(projectType, out var words) ? $"{words} (`Type={projectType}`)" : $"`Type={projectType}`")}");
            block.Append(First("Startup") is { Value.Length: > 0 } startup ? $", startup object `{startup.Value}`.\n" : ".\n");
            block.Append($"- {LegacyText.Plural(All("Form").Count, "form", "forms")}, {LegacyText.Plural(All("Module").Count, "module", "modules")}, {LegacyText.Plural(All("Class").Count, "class", "classes")}.\n");
            if (creatable.Count > 0) block.Append($"- Public creatable classes: {string.Join(", ", creatable)}.\n");
            if (others.Count > 0) block.Append($"- Other classes: {string.Join(", ", others)}.\n");
            if (references.Count > 0) block.Append("- References:\n" + string.Join("\n", references) + "\n");
            if (platform.Count > 0) block.Append($"- Platform references: {string.Join(", ", platform)}.\n");
            LegacyText.Append(c, block.ToString());
        }
    }

    static void Provide(Entry c, string name, string kind, string source)
    {
        if (!(c.Provides ??= []).Any(p => p.Name == name && p.As == kind)) c.Provides.Add(new Provided { Name = name, As = kind, Basis = Basis.Extracted, Source = source });
    }
}
