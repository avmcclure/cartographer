using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Cartographer;

/// <summary>repositories/&lt;repo&gt;/scan-state.yml (spec section 5). Written only by the CLI.</summary>
public sealed class ScanState
{
    public const string FileName = "scan-state.yml";
    const string Header = "# Written by the CLI. Do not edit by hand. Git history of this file is the scan log.\n";

    public string Repository { get; set; } = "";
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string Cartographer { get; set; } = Clock.Version;
    public List<LocationState> Locations { get; set; } = [];
    public SortedDictionary<string, BranchState> Branches { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Entries the repository's mechanical pass owns.</summary>
    public List<string> Entries { get; set; } = [];
    public InProgress? InProgress { get; set; }

    [YamlIgnore] public LocationState? Primary => Locations.FirstOrDefault(l => l.Primary && !l.Gone) ?? Locations.FirstOrDefault(l => !l.Gone);
    [YamlIgnore] public (string Name, BranchState State)? Default => Branches.Where(b => b.Value.Role == "default").Select(b => ((string, BranchState)?)(b.Key, b.Value)).FirstOrDefault();

    public static string PathOf(Atlas atlas, string repositoryFolder) => Path.Combine(atlas.Root, "repositories", repositoryFolder, FileName);

    /// <summary>The stored state, or a new empty one for a repository never seen.</summary>
    public static ScanState Load(Atlas atlas, string repositoryFolder)
    {
        var path = PathOf(atlas, repositoryFolder);
        return File.Exists(path)
            ? EntryFile.Reader.Deserialize<ScanState>(File.ReadAllText(path)) ?? new ScanState { Repository = Ids.Repository(repositoryFolder) }
            : new ScanState { Repository = Ids.Repository(repositoryFolder) };
    }

    public void Save(Atlas atlas)
    {
        var path = PathOf(atlas, Repository.Split('/')[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Header + EntryFile.Yaml.Serialize(this));
    }

    /// <summary>Folder names of every repository that has a state file.</summary>
    public static IEnumerable<string> All(Atlas atlas)
    {
        var dir = Path.Combine(atlas.Root, "repositories");
        return Directory.Exists(dir)
            ? Directory.EnumerateDirectories(dir).Where(d => File.Exists(Path.Combine(d, FileName))).Select(d => Path.GetFileName(d)!).Order(StringComparer.Ordinal)
            : [];
    }
}

public sealed class LocationState
{
    /// <summary>github:&lt;org&gt;/&lt;repo&gt; or azdo:&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;.</summary>
    public string Id { get; set; } = "";
    public bool Primary { get; set; }
    public bool Gone { get; set; }
    public string? DefaultBranch { get; set; }
    /// <summary>Full SHA of the default branch tip last seen there.</summary>
    public string? Tip { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Seen { get; set; }
}

public sealed class BranchState
{
    public string Role { get; set; } = "default";
    public string? ScannedCommit { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Scanned { get; set; }
}

public sealed class InProgress
{
    public string Branch { get; set; } = "";
    public string Commit { get; set; } = "";
    /// <summary>full | changed</summary>
    public string Mode { get; set; } = "full";
    /// <summary>Component slug to done | pending. The mechanical pass sets every component to pending; an agent's work marks them done.</summary>
    public SortedDictionary<string, string> Components { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>The cached local clones of repositories (spec section 7, "clone").</summary>
public static partial class Clones
{
    /// <summary>Directory of the clone cache: CARTOGRAPHER_CACHE, or ~/.cache/cartographer.</summary>
    public static string CacheRoot => Environment.GetEnvironmentVariable("CARTOGRAPHER_CACHE")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "cartographer");
}
