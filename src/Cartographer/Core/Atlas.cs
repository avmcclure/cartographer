using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cartographer;

public sealed class AtlasConfig
{
    public string Organisation { get; set; } = "";
    public ScmConfig? Github { get; set; }
    public ScmConfig? AzureDevops { get; set; }
    public List<string> Exclude { get; set; } = [];
    public List<string> InternalPrefixes { get; set; } = [];
    public Dictionary<string, List<string>> Locations { get; set; } = [];
    public List<InstanceConfig> Instances { get; set; } = [];
    public LimitsConfig Limits { get; set; } = new();

    public static AtlasConfig Parse(string yaml) =>
        new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).Build().Deserialize<AtlasConfig>(yaml) ?? new();
}
public sealed class ScmConfig { public string Organisation { get; set; } = ""; }
public sealed class InstanceConfig
{
    public string Name { get; set; } = "";
    public string Server { get; set; } = "";
    public string Environment { get; set; } = "";
    public bool Production { get; set; }
    /// <summary>Null when the config does not say. A production instance is read only when this is set to true (Db/LiveDb.cs).</summary>
    public bool? Allowed { get; set; }
    public List<string> Databases { get; set; } = [];
}
public sealed class LimitsConfig
{
    public int ScanAgeDays { get; set; } = 90;
    public int QuestionAttempts { get; set; } = 3;
    public int EntryLines { get; set; } = 300;
    public int EntryQuestions { get; set; } = 5;
    public int VerifiedAgeDays { get; set; } = 180;
    public int IndexRows { get; set; } = 200;
}

/// <summary>An atlas on disk: every entry, keyed by ID.</summary>
public sealed class Atlas
{
    public static readonly string[] Folders = ["repositories", "databases", "systems", "channels", "external", "flows"];
    public const string ConfigFile = "cartographer.yml";

    public string Root { get; }
    public AtlasConfig Config { get; private set; } = new();
    public SortedDictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);
    /// <summary>Entry files that could not be parsed: ID to the reason.</summary>
    public SortedDictionary<string, string> Unreadable { get; } = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _onDisk = [];

    Atlas(string root) => Root = Path.GetFullPath(root);

    public static Atlas Load(string root)
    {
        var atlas = new Atlas(root);
        var config = Path.Combine(atlas.Root, ConfigFile);
        if (File.Exists(config)) atlas.Config = AtlasConfig.Parse(File.ReadAllText(config));
        foreach (var folder in Folders)
        {
            var dir = Path.Combine(atlas.Root, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var id = Path.GetRelativePath(atlas.Root, file).Replace('\\', '/')[..^3];
                var text = File.ReadAllText(file);
                try
                {
                    atlas.Entries[id] = EntryFile.Parse(id, text);
                    atlas._onDisk[id] = text;
                }
                catch (EntryFormatException e) { atlas.Unreadable[id] = e.Message; }
            }
        }
        return atlas;
    }

    public string PathOf(string id) => Path.Combine(Root, id.Replace('/', Path.DirectorySeparatorChar) + ".md");

    public Entry? Find(string id) => Entries.GetValueOrDefault(id);

    public IEnumerable<Entry> OfKind(params string[] kinds) => Entries.Values.Where(e => kinds.Contains(e.Kind));

    /// <summary>Writes every entry whose text changed and deletes files of entries that were removed. Returns the IDs written or deleted.</summary>
    public List<string> Save()
    {
        var changed = new List<string>();
        foreach (var (id, entry) in Entries)
        {
            var text = EntryFile.Write(entry);
            if (_onDisk.GetValueOrDefault(id) == text) continue;
            var path = PathOf(id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
            _onDisk[id] = text;
            changed.Add(id);
        }
        foreach (var id in _onDisk.Keys.Where(id => !Entries.ContainsKey(id)).ToList())
        {
            File.Delete(PathOf(id));
            _onDisk.Remove(id);
            changed.Add(id);
        }
        return changed;
    }
}
