namespace Cartographer;

/// <summary>A piece of evidence as a person reads it, with a link to the file when its location is known.</summary>
public sealed record EvidenceLink(string Label, string? Url);

/// <summary>Evidence pointers (spec section 3) as text a person can read and, where a location is known, a link.</summary>
public static class Evidences
{
    public static EvidenceLink? Of(Atlas atlas, Entry entry, string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var hash = source.LastIndexOf("#L", StringComparison.Ordinal);
        var (key, lines) = hash < 0 ? (source, "") : (source[..hash], source[(hash + 1)..]);
        var pointer = entry.Sources?.GetValueOrDefault(key) ?? key;
        var at = pointer.LastIndexOf('@');
        var colon = pointer.IndexOf(':');
        if (at < 0 || colon < 0 || colon > at) return new(source, null);
        var (head, rest, tail) = (pointer[..colon], pointer[(colon + 1)..at], pointer[(at + 1)..]);
        if (head == "person") return new($"stated by {rest}, {tail}", null);
        if (head == "instance") return new($"live catalogue, {rest}, {tail}", null);

        var range = lines.Length == 0 ? [] : lines.Split('-').Select(l => l.TrimStart('L')).ToArray();
        var where = range.Length == 0 ? "" : range.Length == 1 ? $", line {range[0]}" : $", lines {range[0]}-{range[1]}";
        var label = $"{rest[(rest.LastIndexOf('/') + 1)..]} at {tail}{where}";
        var locations = atlas.Find(Ids.Repository(head))?.Locations;
        var location = (locations?.FirstOrDefault(l => l.Primary) ?? locations?.FirstOrDefault())?.Id;
        return new(label, location is null ? null : Url(location, rest, tail, range));
    }

    /// <summary>The page of a file at a commit on GitHub or Azure DevOps.</summary>
    public static string? Url(string location, string path, string commit, string[] lines)
    {
        var p = location[(location.IndexOf(':') + 1)..].Split('/', 3);
        var file = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        if (location.StartsWith("github:") && p.Length == 2)
            return $"https://github.com/{p[0]}/{p[1]}/blob/{commit}/{file}" + (lines.Length == 0 ? "" : "#L" + string.Join("-L", lines));
        if (location.StartsWith("azdo:") && p.Length == 3)
            return $"https://dev.azure.com/{p[0]}/{Uri.EscapeDataString(p[1])}/_git/{Uri.EscapeDataString(p[2])}?path=/{file}&version=GC{commit}" +
                (lines.Length == 0 ? "" : $"&line={lines[0]}&lineEnd={lines[^1]}&lineStartColumn=1&lineEndColumn=200");
        return null;
    }

    /// <summary>The web address of a repository location.</summary>
    public static string? Home(string location)
    {
        var p = location[(location.IndexOf(':') + 1)..].Split('/', 3);
        return location.StartsWith("github:") && p.Length == 2 ? $"https://github.com/{p[0]}/{p[1]}"
            : location.StartsWith("azdo:") && p.Length == 3 ? $"https://dev.azure.com/{p[0]}/{Uri.EscapeDataString(p[1])}/_git/{Uri.EscapeDataString(p[2])}" : null;
    }
}
