namespace Cartographer.Cli;

/// <summary>Clusters of parts that talk to each other (ADR 1), and pinning them.</summary>
public sealed class ClusterCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("clusters", "clusters [--parts]", "The clusters, how well they fit the links, and any regroup worth making.", a =>
        {
            if (a.Positional.Count > 0) throw new UsageException($"unknown clusters command: {a.Positional[0]}. Use pin, unpin, rename or regroup");
            return Show(a.Atlas(), a.Flag("parts"));
        });
        cli.Add("clusters pin", "clusters pin <name> | --all", "Keep a cluster's members and name across scans.", a =>
        {
            if (a.Flag("all") && a.Positional.Count > 0) throw new UsageException("give a cluster name or --all, not both");
            Expect(a, 1, "clusters pin <name>");
            return Pin(a.Atlas(), a.Flag("all") ? null : a.At(0, "name"), true);
        });
        cli.Add("clusters unpin", "clusters unpin <name>", "Let a cluster be grouped afresh on every run.", a => { Expect(a, 1, "clusters unpin <name>"); return Pin(a.Atlas(), a.At(0, "name"), false); });
        cli.Add("clusters rename", "clusters rename <name> <new name>", "Rename a cluster. Renaming pins it.", a => { Expect(a, 2, "clusters rename <name> <new name>"); return Rename(a.Atlas(), a.At(0, "name"), a.At(1, "new name")); });
        cli.Add("clusters regroup", "clusters regroup", "Group every part afresh and pin the result. A cluster holding most of a pinned one keeps its name.", a => { Expect(a, 0, "clusters regroup"); return Regroup(a.Atlas()); });
    }

    public static int Show(Atlas atlas, bool parts)
    {
        var g = Clusters.Current(atlas);
        Console.WriteLine($"{Count(g.Clusters.Count, "cluster")}, {Count(g.Clusters.Sum(c => c.Members.Count), "part")}; " +
            (g.Links == 0 ? "no links between parts yet" : $"{Percent(g.Inside)} of {g.Links} links stay inside a cluster"));
        foreach (var c in g.Clusters)
        {
            Console.WriteLine($"  {c.Name}{(c.Pinned ? " (pinned)" : "")}: {Count(c.Members.Count, "part")}");
            if (parts) foreach (var m in c.Members) Console.WriteLine($"    {m}");
        }
        if (g.Suggestion is { } s) Console.WriteLine(Describe(g, s));
        return 0;
    }

    /// <summary>What a regroup would do, in a sentence or two.</summary>
    public static string Describe(Grouping g, Regroup s)
    {
        var gone = g.Clusters.Where(c => c.Pinned && s.Clusters.All(f => f.Name != c.Name)).ToList();
        var merges = gone.Select(c => $"{c.Name} would {Split(s.Moved.Where(m => m.From == c.Name).GroupBy(m => m.To).OrderByDescending(t => t.Count()).ThenBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Key).ToList())}");
        var singles = s.Moved.Where(m => gone.All(c => c.Name != m.From)).Select(m => $"{m.Part} would move from {m.From} to {m.To}");
        return $"Regrouping would keep {Percent(s.Inside)} of links inside a cluster, up from {Percent(g.Inside)}. " +
               string.Join("; ", merges.Concat(singles)) + ".\nTo regroup: cartographer clusters regroup";
    }

    static string Split(List<string> to) => to.Count == 1 ? $"join {to[0]}" : $"split between {string.Join(", ", to[..^1])} and {to[^1]}";

    // Pin, unpin and rename change only the named cluster in clusters.yaml; every other pin is written back as read.
    static int Pin(Atlas atlas, string? name, bool pin)
    {
        var pins = Clusters.ReadPinned(atlas);
        var now = Clusters.Current(atlas).Clusters;
        if (name is null)
        {
            var added = now.Where(c => !c.Pinned).ToList();
            foreach (var c in added) Clash(pins, c.Name, null);
            Clusters.WritePinned(atlas, [.. pins, .. added.Select(c => c with { Pinned = true })]);
            Console.WriteLine($"pinned {Count(added.Count, "more cluster")}; {Count(pins.Count + added.Count, "cluster")} pinned in all");
            return 0;
        }
        var target = Target(now, pins, name);
        if (pin && !target.Pinned) Clash(pins, target.Name, null);
        if (target.Pinned == pin) { Console.WriteLine($"{target.Name} is already {(pin ? "pinned" : "not pinned")}"); return 0; }
        Clusters.WritePinned(atlas, pin ? [.. pins, target with { Pinned = true }] : pins.Where(p => p.Name != target.Name));
        Console.WriteLine($"{(pin ? "pinned" : "unpinned")} {target.Name}");
        return 0;
    }

    static int Rename(Atlas atlas, string name, string to)
    {
        to = to.Trim();
        if (to.Length == 0) throw new UsageException("the new name is empty");
        var now = Clusters.Current(atlas).Clusters;
        var pins = Clusters.ReadPinned(atlas);
        var target = Target(now, pins, name);
        if (now.Any(c => c != target && c.Name.Equals(to, StringComparison.OrdinalIgnoreCase))) throw new UsageException($"there is already a cluster called {to}");
        Clash(pins, to, target.Pinned ? target.Name : null);
        Clusters.WritePinned(atlas, target.Pinned
            ? pins.Select(p => p.Name == target.Name ? p with { Name = to } : p)
            : [.. pins, target with { Name = to, Pinned = true }]);
        Console.WriteLine($"renamed {target.Name} to {to}{(target.Pinned ? "" : ", and pinned it")}");
        return 0;
    }

    // Regroup pins the whole fresh grouping, so that what it writes is exactly what the suggestion described.
    static int Regroup(Atlas atlas)
    {
        var before = Clusters.Current(atlas);
        var pinnedBefore = Clusters.ReadPinned(atlas).SelectMany(p => p.Members).ToHashSet(StringComparer.Ordinal);
        var fresh = Clusters.Afresh(atlas);
        Clusters.WritePinned(atlas, fresh.Select(c => c with { Pinned = true }));
        var dropped = pinnedBefore.Except(fresh.SelectMany(c => c.Members)).Order(StringComparer.Ordinal).ToList();
        var after = Clusters.Current(atlas);
        Console.WriteLine($"regrouped and pinned {Count(after.Clusters.Count, "cluster")}" +
            (after.Links == 0 ? "" : $": {Percent(after.Inside)} of links stay inside a cluster, was {Percent(before.Inside)}"));
        // Unpinned clusters were renamed freely on every run, so only parts that left a pinned cluster have moved.
        foreach (var m in Clusters.Moves(before.Clusters, after.Clusters).Where(m => before.Clusters.Any(c => c.Pinned && c.Name == m.From))) Console.WriteLine($"  {m.Part}: {m.From} -> {m.To}");
        foreach (var d in dropped) Console.WriteLine($"  {d}: missing now, and its cluster was not kept; it will be grouped afresh when it returns");
        return 0;
    }

    static void Expect(Args a, int count, string usage)
    {
        if (a.Positional.Count > count) throw new UsageException($"too many arguments: {usage}. Put a name with spaces in quotes");
    }

    // The clusters shown now, then pins whose parts are all missing right now: not listed, but still renamable and unpinnable.
    static Cluster Target(IReadOnlyList<Cluster> now, List<Cluster> pins, string name)
    {
        name = name.Trim();
        List<Cluster> all = [.. now, .. pins.Where(p => now.All(c => c.Name != p.Name))];
        return all[Find(all, name)];
    }

    // A pin whose parts are all missing right now is not listed, but its name is still taken.
    static void Clash(List<Cluster> pins, string name, string? self)
    {
        if (pins.Any(p => p.Name != self && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new UsageException($"a pinned cluster is already called {name}, though its parts are missing right now. Choose another name, or unpin it with: cartographer clusters unpin \"{name}\"");
    }

    static int Find(List<Cluster> clusters, string name)
    {
        var i = clusters.FindIndex(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : throw new UsageException($"no cluster called {name}. Run `cartographer clusters` to list them");
    }

    static string Percent(double x) => $"{Math.Round(x * 100, MidpointRounding.AwayFromZero)}%";
    static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}
