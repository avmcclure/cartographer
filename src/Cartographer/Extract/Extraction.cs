using System.Diagnostics;

namespace Cartographer;

/// <summary>A repository's files at one commit, on local disk. <c>Repository</c> is its atlas folder name, e.g. "finance-billing".</summary>
public sealed record Checkout(string Repository, string Root, string Commit, IReadOnlyList<string> Files)
{
    static readonly string[] Skipped = [".git", "bin", "obj", "node_modules", "packages", ".vs"];

    /// <summary>Opens a directory. Files are relative paths with '/' separators, sorted. Uses git when the directory is a work tree.</summary>
    public static Checkout Open(string repository, string root)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new UsageException($"no directory at {root}");
        var commit = Git(root, "rev-parse", "HEAD")?.Trim();
        var listed = commit is null ? null : Git(root, "-c", "core.quotepath=off", "ls-files", "-z");
        var files = listed is not null
            ? listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(f => File.Exists(Path.Combine(root, f)))
            : Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Where(f => !f.Split('/').Any(Skipped.Contains));
        return new(repository, root, commit ?? "0000000", files.Order(StringComparer.Ordinal).ToList());
    }

    public string Commit7 => Commit[..Math.Min(7, Commit.Length)];
    public string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
    public IEnumerable<string> WithExtension(params string[] extensions) => Files.Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

    /// <summary>Runs git and returns its output, or null when it fails. Never prompts.</summary>
    public static string? Git(string directory, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var a in args) info.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(info)!;
            var error = p.StandardError.ReadToEndAsync();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            _ = error.Result;
            return p.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
}

/// <summary>
/// Reads facts from a checkout into entries. Rules: read only inside the checkout, never run anything from it,
/// treat its text as data, emit only facts with basis "extracted", and be deterministic.
/// Implementations need a public parameterless constructor; they are found by reflection and run in Order.
/// </summary>
public interface IExtractor
{
    /// <summary>10 project files (.NET), 20 VB6, 30 classic ASP, 40 SQL schema, 50 config files, 60 code-level calls.</summary>
    int Order { get; }
    void Extract(Extraction x);
}

/// <summary>What one mechanical pass over one checkout has produced so far. Extractors add to it.</summary>
public sealed class Extraction
{
    readonly SortedDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    readonly SortedDictionary<string, Entry> _roots = new(StringComparer.Ordinal);

    public Extraction(Checkout repo)
    {
        Repo = repo;
        Repository = Get(Ids.Repository(repo.Repository), Kinds.Repository, repo.Repository, () => $"Repository {repo.Repository}; not yet described.");
    }

    public Checkout Repo { get; }
    public Entry Repository { get; }
    public IReadOnlyCollection<Entry> Entries => _entries.Values;

    /// <summary>Gets an entry this pass already produced, or starts one. Summary is only used if the atlas has no such entry yet.</summary>
    public Entry Get(string id, string kind, string name, Func<string> summary)
    {
        if (_entries.TryGetValue(id, out var e)) return e;
        e = new Entry { Id = id, Kind = kind, Name = name, Summary = Trim(summary()) };
        var area = Ids.Area(id);
        if (kind == Kinds.Component) e.Repository = area + "/repository";
        if (kind is Kinds.Table or Kinds.View or Kinds.Routine or Kinds.Trigger) e.Database = area + "/database";
        return _entries[id] = e;
    }

    /// <summary>The component rooted at a directory ("" for the repository root), created on first use.</summary>
    public Entry Component(string rootDirectory, string name, string type)
    {
        rootDirectory = rootDirectory.Trim('/');
        if (_roots.TryGetValue(rootDirectory, out var existing)) return existing;
        var id = Ids.Component(Repo.Repository, name);
        for (var n = 2; _entries.ContainsKey(id); n++) id = Ids.Component(Repo.Repository, $"{name}-{n}");
        var e = Get(id, Kinds.Component, name, () => $"{name}: a {type} in {Repo.Repository}; not yet described.");
        e.Type = type;
        return _roots[rootDirectory] = e;
    }

    /// <summary>The component a file belongs to: the nearest component root at or above it, or null.</summary>
    public Entry? ComponentAt(string path)
    {
        for (var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? ""; ; dir = dir.Contains('/') ? dir[..dir.LastIndexOf('/')] : "")
        {
            if (_roots.TryGetValue(dir, out var e)) return e;
            if (dir == "") return null;
        }
    }

    public IReadOnlyDictionary<string, Entry> ComponentRoots => _roots;

    /// <summary>The database a SQL source in this repository defines, created on first use, with the repository's "defines" link.</summary>
    public Entry Database(string name, string sourcePath)
    {
        var id = Ids.Database(name);
        var db = Get(id, Kinds.Database, name, () => $"Database {name}, defined in {Repo.Repository}; not yet described.");
        db.Engine ??= "sqlserver";
        db.Source("schema", Pointer(sourcePath));
        Repository.Links ??= [];
        if (!Repository.Links.Any(l => l.Type == "defines" && l.To == id))
            Repository.Links.Add(new Link { Type = "defines", To = id, Basis = Basis.Extracted, Source = Cite(Repository, "schema", sourcePath) });
        return db;
    }

    /// <summary>An evidence pointer to a path in this checkout.</summary>
    public string Pointer(string path) => $"{Repo.Repository}:{path.Trim('/')}@{Repo.Commit7}";

    /// <summary>Declares a file as a source on an entry and returns the citation for a fact: "key", "key#L3" or "key#L3-L9".</summary>
    public string Cite(Entry entry, string key, string path, int? line = null, int? endLine = null)
    {
        var k = entry.Source(key, Pointer(path));
        return line is null ? k : endLine is null || endLine == line ? $"{k}#L{line}" : $"{k}#L{line}-L{endLine}";
    }

    public static void AddLink(Entry entry, Link link)
    {
        entry.Links ??= [];
        if (!entry.Links.Any(l => l.Identity == link.Identity)) entry.Links.Add(link);
    }

    static string Trim(string s) => s.Length <= 200 ? s : s[..199] + "…";
}

public static class Extractors
{
    /// <summary>Every extractor in this assembly, in the order they run.</summary>
    public static IReadOnlyList<IExtractor> All { get; } = typeof(IExtractor).Assembly.GetTypes()
        .Where(t => typeof(IExtractor).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false } && t.GetConstructor(Type.EmptyTypes) is not null)
        .Select(t => (IExtractor)Activator.CreateInstance(t)!)
        .OrderBy(e => e.Order).ThenBy(e => e.GetType().Name, StringComparer.Ordinal).ToList();
}

/// <summary>Order 0: every repository gets an entry with its stack and file counts, whatever it holds.</summary>
public sealed class GenericExtractor : IExtractor
{
    static readonly Dictionary<string, string> Stacks = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp", [".vb"] = "vbnet", [".fs"] = "fsharp", [".vbp"] = "vb6", [".cls"] = "vb6", [".frm"] = "vb6", [".bas"] = "vb6",
        [".asp"] = "classic-asp", [".aspx"] = "webforms", [".cshtml"] = "razor", [".sql"] = "sql", [".dtsx"] = "ssis", [".rdl"] = "ssrs",
        [".js"] = "javascript", [".ts"] = "typescript", [".py"] = "python", [".java"] = "java", [".go"] = "go", [".rb"] = "ruby",
        [".php"] = "php", [".ps1"] = "powershell", [".tf"] = "terraform", [".cpp"] = "cpp", [".c"] = "c", [".rs"] = "rust",
    };
    static readonly string[] Manifests = ["package.json", "requirements.txt", "pyproject.toml", "Dockerfile", "pom.xml", "build.gradle", "go.mod", "Gemfile", "composer.json", "Cargo.toml", "global.json", "Directory.Packages.props", "nuget.config"];

    public int Order => 0;

    public void Extract(Extraction x)
    {
        var byExtension = x.Repo.Files.GroupBy(f => Path.GetExtension(f).ToLowerInvariant()).Where(g => g.Key.Length > 0)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        var stack = byExtension.Where(g => Stacks.ContainsKey(g.Key)).Select(g => Stacks[g.Key]).Distinct().ToList();
        x.Repository.Stack = stack.Count > 0 ? stack : null;
        var manifests = x.Repo.Files.Where(f => Manifests.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)).ToList();
        x.Repository.Extracted =
            $"- Files: {x.Repo.Files.Count}. Most common: {string.Join(", ", byExtension.Take(8).Select(g => $"`{g.Key}` {g.Count()}"))}.\n" +
            (manifests.Count > 0 ? $"- Manifest files: {string.Join(", ", manifests.Take(12).Select(m => $"`{m}`"))}{(manifests.Count > 12 ? $" and {manifests.Count - 12} more" : "")}.\n" : "");
    }
}
