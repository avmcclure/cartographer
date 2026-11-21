using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Cartographer;

/// <summary>One entry in an atlas. Property order is the frontmatter key order (spec section 3).</summary>
public sealed class Entry
{
    [YamlIgnore] public string Id { get; set; } = "";
    [YamlIgnore] public string Body { get; set; } = "";
    /// <summary>Set by extractors: the text for the body's extracted block. Never read from disk.</summary>
    [YamlIgnore] public string? Extracted { get; set; }

    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string Summary { get; set; } = "";
    public string? Status { get; set; }
    public string? Repository { get; set; }
    public string? Database { get; set; }
    public string? Engine { get; set; }
    public List<string>? Stack { get; set; }
    public List<Location>? Locations { get; set; }
    public List<string>? Aliases { get; set; }
    public List<Instance>? Instances { get; set; }
    public SortedDictionary<string, string>? Sources { get; set; }
    public List<Provided>? Provides { get; set; }
    public List<Membership>? Systems { get; set; }
    public List<Tag>? Tags { get; set; }
    public Owner? Owner { get; set; }
    public List<string>? Packages { get; set; }
    public List<Link>? Links { get; set; }
    public List<Drift>? Drift { get; set; }
    public Verified? Verified { get; set; }

    /// <summary>Registers a source pointer under a key and returns the key, reusing a key that already holds it.</summary>
    public string Source(string key, string pointer)
    {
        Sources ??= new(StringComparer.Ordinal);
        foreach (var (k, v) in Sources) if (v == pointer) return k;
        var unique = key;
        for (var n = 2; Sources.ContainsKey(unique); n++) unique = $"{key}-{n}";
        Sources[unique] = pointer;
        return unique;
    }
}

public sealed class Location { public string Id { get; set; } = ""; public bool Primary { get; set; } }
public sealed class Instance
{
    public string Name { get; set; } = "";
    public string? Environment { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Read { get; set; }
    public string? Permission { get; set; }
}
public sealed class Provided { public string Name { get; set; } = ""; public string As { get; set; } = ""; public string Basis { get; set; } = ""; public string? Source { get; set; } }
public sealed class Membership { public string Id { get; set; } = ""; public string Basis { get; set; } = ""; public string? Source { get; set; } }
public sealed class Tag { public string Name { get; set; } = ""; public string Basis { get; set; } = ""; public string? Source { get; set; } }
public sealed class Owner { public string Name { get; set; } = ""; public string Basis { get; set; } = ""; public string? Source { get; set; } }
public sealed class Drift { public string What { get; set; } = ""; public List<string> Sources { get; set; } = []; }
public sealed class Verified
{
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Commit { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string Date { get; set; } = "";
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string Cartographer { get; set; } = "";
}

public sealed class Link
{
    public string Type { get; set; } = "";
    public string? To { get; set; }
    public string? Name { get; set; }
    public string? Database { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Version { get; set; }
    public string? How { get; set; }
    public string? Via { get; set; }
    public List<string>? Uses { get; set; }
    public int? Count { get; set; }
    public string Basis { get; set; } = "";
    public string? Source { get; set; }

    /// <summary>What makes two links the same link, whatever their evidence.</summary>
    [YamlIgnore] public string Identity => $"{Type}|{To}|{(To is null || Type == "references" ? Name : null)}|{(To is null ? Database : null)}|{Via}".ToLowerInvariant();
}

public static class Basis
{
    public const string Extracted = "extracted", Inferred = "inferred", Stated = "stated";
    public static readonly string[] All = [Extracted, Inferred, Stated];
}

public static class Kinds
{
    public const string Repository = "repository", Component = "component", Database = "database", Table = "table", View = "view",
        Routine = "routine", Trigger = "trigger", System = "system", Channel = "channel", External = "external", Flow = "flow";

    /// <summary>Kind to the pattern its ID must match.</summary>
    public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        [Repository] = "^repositories/[^/]+/repository$",
        [Component] = "^repositories/[^/]+/components/[^/]+$",
        [Database] = "^databases/[^/]+/database$",
        [Table] = "^databases/[^/]+/tables/[^/]+/[^/]+$",
        [View] = "^databases/[^/]+/tables/[^/]+/[^/]+$",
        [Routine] = "^databases/[^/]+/routines/[^/]+/[^/]+$",
        [Trigger] = "^databases/[^/]+/triggers/[^/]+/[^/]+$",
        [System] = "^systems/[^/]+$",
        [Channel] = "^channels/[^/]+$",
        [External] = "^external/[^/]+$",
        [Flow] = "^flows/[^/]+$",
    };

    public static readonly IReadOnlyDictionary<string, string[]> Types = new Dictionary<string, string[]>
    {
        [Component] = ["library", "service", "site", "desktop-app", "job", "etl", "report"],
        [Routine] = ["procedure", "function"],
        [Channel] = ["queue", "topic", "file-drop"],
    };
}

public static class LinkTypes
{
    /// <summary>Link type to the kinds it may start from and end at.</summary>
    public static readonly IReadOnlyDictionary<string, (string[] From, string[] To)> Rules = new Dictionary<string, (string[], string[])>
    {
        ["depends-on"] = ([Kinds.Component], [Kinds.Component]),
        ["calls"] = ([Kinds.Component, Kinds.Routine, Kinds.Trigger, Kinds.View], [Kinds.Component, Kinds.Routine]),
        ["reads"] = ([Kinds.Component, Kinds.Routine, Kinds.Trigger, Kinds.View], [Kinds.Table, Kinds.View]),
        ["writes"] = ([Kinds.Component, Kinds.Routine, Kinds.Trigger, Kinds.View], [Kinds.Table, Kinds.View]),
        ["uses"] = ([Kinds.Component], [Kinds.Database]),
        ["publishes-to"] = ([Kinds.Component], [Kinds.Channel]),
        ["consumes-from"] = ([Kinds.Component], [Kinds.Channel]),
        ["sends-to"] = ([Kinds.Component], [Kinds.External]),
        ["receives-from"] = ([Kinds.Component], [Kinds.External]),
        ["links-to"] = ([Kinds.Database], [Kinds.Database]),
        ["references"] = ([Kinds.Table], [Kinds.Table]),
        ["fires-on"] = ([Kinds.Trigger], [Kinds.Table, Kinds.View]),
        ["defines"] = ([Kinds.Repository], [Kinds.Database]),
    };
}

public static class Ids
{
    /// <summary>A path segment: lower-case, anything outside [a-z0-9._-] becomes '-'.</summary>
    public static string Slug(string text)
    {
        var chars = text.Trim().ToLowerInvariant().Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-' ? c : '-').ToArray();
        var s = System.Text.RegularExpressions.Regex.Replace(new string(chars), "-{2,}", "-").Trim('-', '.');
        return s.Length == 0 ? "unnamed" : s;
    }

    public static string Repository(string folder) => $"repositories/{folder}/repository";
    public static string Component(string repositoryFolder, string name) => $"repositories/{repositoryFolder}/components/{Slug(name)}";
    public static string Database(string name) => $"databases/{Slug(name)}/database";
    public static string DbObject(string database, string folder, string schema, string name) => $"databases/{Slug(database)}/{folder}/{Slug(schema)}/{Slug(name)}";

    /// <summary>The repository folder or database folder an ID sits under, or null.</summary>
    public static string? Area(string id)
    {
        var p = id.Split('/');
        return p.Length >= 2 && p[0] is "repositories" or "databases" ? $"{p[0]}/{p[1]}" : null;
    }
}

public static class Clock
{
    /// <summary>Today, or CARTOGRAPHER_DATE when set, so tests and stored expected output repeat.</summary>
    public static string Today => Environment.GetEnvironmentVariable("CARTOGRAPHER_DATE") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
    public const string Version = "0.1.0";
}
