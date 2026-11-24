using YamlDotNet.Core;

namespace Cartographer;

/// <summary>A group of parts that talk to each other more than to the rest (ADR 1).</summary>
public sealed record Cluster(string Name, bool Pinned, IReadOnlyList<string> Members);

/// <summary>Clusters as the atlas keeps them, with how well they fit the links and any regroup worth suggesting.</summary>
public sealed record Grouping(IReadOnlyList<Cluster> Clusters, int Links, double Inside, Regroup? Suggestion);

/// <summary>A fresh grouping that fits the current links clearly better than the pinned one.</summary>
/// <param name="Moved">Parts that would land in a different cluster, with the names of the old and new one.</param>
public sealed record Regroup(IReadOnlyList<Cluster> Clusters, double Inside, IReadOnlyList<(string Part, string From, string To)> Moved);

/// <summary>
/// Groups parts by how they interact: Louvain community detection over the links between parts, one weight per
/// link. People pin clusters in clusters.yaml; a pinned cluster keeps its members and name, and parts in no pinned
/// cluster are grouped afresh on every run. A regroup is suggested when grouping everything afresh keeps
/// <see cref="ThresholdPoints"/> percentage points more of the links inside clusters and moves at least one part.
/// </summary>
public static class Clusters
{
    /// <summary>How many percentage points more of the links a fresh grouping must keep inside clusters before a regroup is suggested.</summary>
    public const int ThresholdPoints = 3;
    public const string FileName = "clusters.yaml";

    sealed class File_ { public List<Pin> Clusters { get; set; } = []; }
    sealed class Pin { public string Name { get; set; } = ""; public List<string> Members { get; set; } = []; }

    /// <summary>The part an entry counts as: tables, views, routines and triggers count as their database. Null for
    /// what is not a part (repositories, systems, flows).</summary>
    public static string? PartOf(Atlas atlas, string id)
    {
        if (atlas.Find(id) is not { } e) return null;
        return e.Kind switch
        {
            Kinds.Component or Kinds.Database or Kinds.Channel or Kinds.External => e.Id,
            Kinds.Table or Kinds.View or Kinds.Routine or Kinds.Trigger => atlas.Find(Ids.Area(e.Id) + "/database")?.Id,
            _ => null,
        };
    }

    /// <summary>Every part, in ID order, and every link between two different parts.</summary>
    public static (List<string> Parts, List<(string A, string B)> Links) Graph(Atlas atlas)
    {
        var parts = atlas.Entries.Keys.Where(id => PartOf(atlas, id) == id).Order(StringComparer.Ordinal).ToList();
        var links = new List<(string, string)>();
        foreach (var e in atlas.Entries.Values.OrderBy(e => e.Id, StringComparer.Ordinal))
            foreach (var l in e.Links ?? [])
                if (l.To is not null && PartOf(atlas, e.Id) is { } a && PartOf(atlas, l.To) is { } b && a != b) links.Add((a, b));
        return (parts, links);
    }

    public static List<Cluster> ReadPinned(Atlas atlas)
    {
        var path = Path.Combine(atlas.Root, FileName);
        if (!File.Exists(path)) return [];
        File_? file;
        try { file = EntryFile.Reader.Deserialize<File_>(File.ReadAllText(path)) ?? new(); }
        catch (YamlException e) { throw new UsageException($"{FileName} line {e.Start.Line}: {e.InnerException?.Message ?? e.Message}"); }
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Cluster>();
        foreach (var p in file?.Clusters ?? [])
        {
            var name = p?.Name?.Trim() ?? "";
            if (name.Length == 0) throw new UsageException($"{FileName}: a cluster has no name");
            if (!names.Add(name)) throw new UsageException($"{FileName}: two clusters are called {name}");
            if (p!.Members is null || p.Members.Count == 0) throw new UsageException($"{FileName}: {name} has no members");
            var members = p.Members.Select(m => m?.Trim()).ToList();
            foreach (var m in members)
            {
                if (string.IsNullOrWhiteSpace(m)) throw new UsageException($"{FileName}: {name} has an empty member");
                if (atlas.Find(m) is not null && PartOf(atlas, m) != m)
                    throw new UsageException($"{FileName}: {m} in {name} is not a part{(PartOf(atlas, m) is { } whole ? $"; use {whole}" : "")}");
                if (!seen.TryAdd(m!, name) && seen[m!] != name) throw new UsageException($"{FileName}: {m} is in both {seen[m]} and {name}");
            }
            result.Add(new Cluster(name, true, [.. members.Distinct()!]));
        }
        return result;
    }

    public static void WritePinned(Atlas atlas, IEnumerable<Cluster> clusters)
    {
        var pinned = clusters.Where(c => c.Pinned).Select(c => new Pin { Name = c.Name, Members = [.. c.Members.Order(StringComparer.Ordinal)] }).ToList();
        var path = Path.Combine(atlas.Root, FileName);
        if (pinned.Count == 0) { File.Delete(path); return; }
        File.WriteAllText(path, "# Pinned clusters. Written by `cartographer clusters`; parts in none of these are grouped afresh on every run.\n" +
            EntryFile.Yaml.Serialize(new File_ { Clusters = pinned }));
    }

    /// <summary>The clusters now: the pinned ones, minus parts that are gone, and the other parts grouped afresh.</summary>
    public static Grouping Current(Atlas atlas)
    {
        var (parts, links) = Graph(atlas);
        var known = parts.ToHashSet(StringComparer.Ordinal);
        var pins = ReadPinned(atlas);
        var pinned = pins
            .Select(c => c with { Members = [.. c.Members.Where(known.Contains).Order(StringComparer.Ordinal)] })
            .Where(c => c.Members.Count > 0).ToList();
        var placed = pinned.SelectMany(c => c.Members).ToHashSet(StringComparer.Ordinal);
        var rest = parts.Where(p => !placed.Contains(p)).ToList();
        var restLinks = links.Where(l => !placed.Contains(l.A) && !placed.Contains(l.B)).ToList();
        var clusters = Named(atlas, [.. pinned, .. Fresh(rest, restLinks).Select(m => new Cluster("", false, m))], pins, known, links);

        Regroup? suggestion = null;
        if (pinned.Count > 0)
        {
            var fresh = Named(atlas, [.. Fresh(parts, links).Select(m => new Cluster("", false, m))], pins, known, links);
            // Whole link counts, so that exactly three points is not lost to rounding.
            var (now, then) = (InsideCount(clusters, links), InsideCount(fresh, links));
            var moved = Moves(clusters, fresh);
            if (then > now && (then - now) * 100 >= ThresholdPoints * links.Count && moved.Count > 0) suggestion = new(fresh, Inside(fresh, links), moved);
        }
        return new(clusters, links.Count, Inside(clusters, links), suggestion);
    }

    /// <summary>Every part grouped afresh, ignoring pins. A cluster holding more than half of a pinned one keeps its name and pin; the rest are named after their busiest part.</summary>
    public static List<Cluster> Afresh(Atlas atlas)
    {
        var (parts, links) = Graph(atlas);
        var known = parts.ToHashSet(StringComparer.Ordinal);
        var pins = ReadPinned(atlas);
        var fresh = Named(atlas, [.. Fresh(parts, links).Select(m => new Cluster("", false, m))], pins, known, links);
        // Pinned parts that are missing right now (a repository mid-rescan) go with the cluster that inherits their
        // pin's name. A pin that no cluster inherits is gone, and so are its missing parts.
        foreach (var pin in pins)
        {
            var i = fresh.FindIndex(c => c.Pinned && c.Name == pin.Name);
            if (i >= 0) fresh[i] = fresh[i] with { Members = [.. fresh[i].Members, .. pin.Members.Where(m => !known.Contains(m))] };
        }
        return fresh;
    }

    /// <summary>The share of links whose two ends are in the same cluster.</summary>
    public static double Inside(IReadOnlyList<Cluster> clusters, IReadOnlyList<(string A, string B)> links) =>
        links.Count == 0 ? 1 : (double)InsideCount(clusters, links) / links.Count;

    static int InsideCount(IReadOnlyList<Cluster> clusters, IReadOnlyList<(string A, string B)> links)
    {
        var of = Of(clusters);
        return links.Count(l => of.TryGetValue(l.A, out var a) && of.TryGetValue(l.B, out var b) && a == b);
    }

    static Dictionary<string, int> Of(IReadOnlyList<Cluster> clusters)
    {
        var of = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < clusters.Count; i++) foreach (var m in clusters[i].Members) of[m] = i;
        return of;
    }

    /// <summary>Parts that land in a different cluster, where the old or the new one is pinned.</summary>
    public static List<(string Part, string From, string To)> Moves(IReadOnlyList<Cluster> from, IReadOnlyList<Cluster> to)
    {
        var (a, b) = (Of(from), Of(to));
        // Unpinned clusters are regrouped on every run anyway, so only moves into or out of a pinned cluster count.
        return [.. a.Keys.Order(StringComparer.Ordinal)
            .Where(p => b.ContainsKey(p) && from[a[p]].Name != to[b[p]].Name && (from[a[p]].Pinned || to[b[p]].Pinned))
            .Select(p => (p, from[a[p]].Name, to[b[p]].Name))];
    }

    /// <summary>
    /// Names clusters that have none. A fresh cluster that holds more than half of a pinned cluster's parts takes its
    /// name and pin (at most one can); otherwise it is named after its busiest part and is not pinned.
    /// </summary>
    static List<Cluster> Named(Atlas atlas, List<Cluster> clusters, List<Cluster> pins, HashSet<string> known, List<(string A, string B)> links)
    {
        var degree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (a, b) in links) { degree[a] = degree.GetValueOrDefault(a) + 1; degree[b] = degree.GetValueOrDefault(b) + 1; }
        // Every pinned name is reserved: only the overlap rule below can give one out, so a name always means one pin.
        var taken = clusters.Where(c => c.Name != "").Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reserved = pins.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pinOf = pins.SelectMany(p => p.Members.Select(m => (m, p.Name))).ToDictionary(x => x.m, x => x.Name, StringComparer.Ordinal);
        var present = pins.ToDictionary(p => p.Name, p => p.Members.Count(known.Contains), StringComparer.Ordinal);
        var overlaps = (from i in Enumerable.Range(0, clusters.Count) where clusters[i].Name == ""
                        from hit in clusters[i].Members.Where(pinOf.ContainsKey).GroupBy(m => pinOf[m])
                        let n = hit.Count()
                        where 2 * n > present[hit.Key]
                        select (i, Name: hit.Key, n)).OrderByDescending(x => x.n).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        var result = clusters.ToList();
        foreach (var (i, name, _) in overlaps)
            if (result[i].Name == "" && !taken.Contains(name)) { taken.Add(name); result[i] = result[i] with { Name = name, Pinned = true }; }
        taken.UnionWith(reserved);
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].Name != "") continue;
            var busiest = result[i].Members.OrderByDescending(m => degree.GetValueOrDefault(m)).ThenBy(m => m, StringComparer.Ordinal).First();
            var label = atlas.Entries[busiest].Name?.Trim() is { Length: > 0 } n ? n : busiest;
            var name = label;
            for (var k = 2; !taken.Add(name); k++) name = $"{label} ({k})";
            result[i] = result[i] with { Name = name };
        }
        return [.. result.OrderByDescending(c => c.Members.Count).ThenBy(c => c.Name, StringComparer.Ordinal)];
    }

    /// <summary>Parts grouped afresh; each list in ID order, the lists in order of their first part.</summary>
    public static List<List<string>> Fresh(IReadOnlyList<string> parts, IReadOnlyList<(string A, string B)> links)
    {
        var index = parts.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i, StringComparer.Ordinal);
        var edges = links.Where(l => index.ContainsKey(l.A) && index.ContainsKey(l.B)).Select(l => (index[l.A], index[l.B])).ToList();
        var community = Louvain(parts.Count, edges);
        return [.. parts.Select((p, i) => (p, c: community[i])).GroupBy(x => x.c).Select(g => g.Select(x => x.p).ToList()).OrderBy(g => g[0], StringComparer.Ordinal)];
    }

    /// <summary>
    /// Blondel, Guillaume, Lambiotte and Lefebvre, "Fast unfolding of communities in large networks" (2008): move each
    /// node to the neighbouring community that most raises modularity until nothing moves, then merge each community
    /// into one node and repeat. Nodes are visited in index order; on a tie a node stays where it is, and otherwise
    /// goes to the lowest community, so the result depends only on the input.
    /// </summary>
    public static int[] Louvain(int count, IReadOnlyList<(int A, int B)> links)
    {
        var member = Enumerable.Range(0, count).ToArray();       // original node -> node at the current level
        var weights = new Dictionary<(int, int), double>();
        foreach (var (a, b) in links) Add(weights, a, b, 1);
        var n = count;
        for (var level = 0; level < 32; level++)
        {
            var adjacent = Enumerable.Range(0, n).Select(_ => new SortedDictionary<int, double>()).ToArray();
            var self = new double[n];
            double twiceTotal = 0;
            foreach (var ((a, b), w) in weights)
            {
                twiceTotal += 2 * w;
                if (a == b) { self[a] += w; continue; }
                adjacent[a][b] = adjacent[a].GetValueOrDefault(b) + w;
                adjacent[b][a] = adjacent[b].GetValueOrDefault(a) + w;
            }
            if (twiceTotal == 0) break;
            var degree = Enumerable.Range(0, n).Select(v => adjacent[v].Values.Sum() + 2 * self[v]).ToArray();
            var community = Enumerable.Range(0, n).ToArray();
            var total = (double[])degree.Clone();
            var any = false;
            for (int pass = 0, moved = 1; moved > 0 && pass < 100; pass++)
            {
                moved = 0;
                for (var v = 0; v < n; v++)
                {
                    var home = community[v];
                    var toward = new SortedDictionary<int, double>();
                    foreach (var (u, w) in adjacent[v]) toward[community[u]] = toward.GetValueOrDefault(community[u]) + w;
                    total[home] -= degree[v];
                    var (best, gain) = (home, toward.GetValueOrDefault(home) - total[home] * degree[v] / twiceTotal);
                    foreach (var (c, w) in toward)
                    {
                        var g = w - total[c] * degree[v] / twiceTotal;
                        if (g > gain + 1e-9) (best, gain) = (c, g);
                    }
                    total[best] += degree[v];
                    if (best != home) { community[v] = best; moved++; any = true; }
                }
            }
            if (!any) break;
            var renumber = new Dictionary<int, int>();
            foreach (var c in community) if (!renumber.ContainsKey(c)) renumber[c] = renumber.Count;
            var next = new Dictionary<(int, int), double>();
            foreach (var ((a, b), w) in weights) Add(next, renumber[community[a]], renumber[community[b]], w);
            member = [.. member.Select(v => renumber[community[v]])];
            (weights, n) = (next, renumber.Count);
        }
        return member;

        static void Add(Dictionary<(int, int), double> map, int a, int b, double w)
        {
            var key = a <= b ? (a, b) : (b, a);
            map[key] = map.GetValueOrDefault(key) + w;
        }
    }
}
