namespace Cartographer.Tests;

/// <summary>Clusters of parts by how they interact (ADR 1).</summary>
public class ClusterTests
{
    // Two tight groups of four services, joined by one link: orders a–d and stock e–h.
    static Atlas TwoGroups()
    {
        var dir = Directory.CreateTempSubdirectory("clusters-").FullName;
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile), "organisation: Test\n");
        var atlas = Atlas.Load(dir);
        foreach (var n in "abcdefgh") Add(atlas, $"svc-{n}");
        foreach (var group in new[] { "abcd", "efgh" })
            for (var i = 0; i < 4; i++) for (var j = i + 1; j < 4; j++) Link(atlas, $"svc-{group[i]}", $"svc-{group[j]}");
        Link(atlas, "svc-d", "svc-e");
        return atlas;
    }

    static string Id(string name) => Ids.Component(name, name);

    static void Add(Atlas atlas, string name) =>
        atlas.Entries[Id(name)] = new Entry { Id = Id(name), Kind = Kinds.Component, Type = "service", Name = name, Summary = name, Repository = Ids.Repository(name) };

    static void Link(Atlas atlas, string from, string to) =>
        (atlas.Entries[Id(from)].Links ??= []).Add(new Link { Type = "calls", To = Id(to), Basis = Basis.Extracted });

    static string[] Members(Grouping g, string part) => [.. g.Clusters.Single(c => c.Members.Contains(Id(part))).Members];

    [Fact]
    public void Louvain_finds_two_groups_joined_by_one_link()
    {
        int[] community = Clusters.Louvain(6, [(0, 1), (1, 2), (0, 2), (3, 4), (4, 5), (3, 5), (2, 3)]);
        Assert.Equal(community[0], community[1]);
        Assert.Equal(community[1], community[2]);
        Assert.Equal(community[3], community[5]);
        Assert.NotEqual(community[0], community[3]);
        Assert.Equal(community, Clusters.Louvain(6, [(0, 1), (1, 2), (0, 2), (3, 4), (4, 5), (3, 5), (2, 3)]));
    }

    [Fact]
    public void Parts_are_grouped_by_their_links_and_named_after_the_busiest()
    {
        var g = Clusters.Current(TwoGroups());
        Assert.Equal(2, g.Clusters.Count);
        Assert.Equal(["abcd", "efgh"], g.Clusters.Select(c => string.Concat(c.Members.Select(m => m[^1]))).Order());
        Assert.Equal(12.0 / 13, g.Inside, 3);
        Assert.Null(g.Suggestion);
        Assert.All(g.Clusters, c => Assert.False(c.Pinned));
        Assert.Contains(g.Clusters, c => c.Name == "svc-d");   // d has the most links in its group
    }

    [Fact]
    public void A_table_counts_as_its_database()
    {
        var atlas = TwoGroups();
        var table = "databases/stock/tables/dbo/items";
        atlas.Entries["databases/stock/database"] = new Entry { Id = "databases/stock/database", Kind = Kinds.Database, Name = "Stock" };
        atlas.Entries[table] = new Entry { Id = table, Kind = Kinds.Table, Name = "dbo.items" };
        foreach (var n in "efg") Link(atlas, $"svc-{n}", "dummy");
        foreach (var n in "efg") atlas.Entries[Id($"svc-{n}")].Links![^1].To = table;
        var (parts, links) = Clusters.Graph(atlas);
        Assert.Contains("databases/stock/database", parts);
        Assert.DoesNotContain(table, parts);
        Assert.Contains("databases/stock/database", Members(Clusters.Current(atlas), "svc-e"));
        Assert.Equal(16, links.Count);
    }

    [Fact]
    public void A_pinned_and_renamed_cluster_keeps_its_name_and_members()
    {
        var atlas = TwoGroups();
        var first = Clusters.Current(atlas).Clusters;
        Clusters.WritePinned(atlas, first.Select(c => c.Members.Contains(Id("svc-a")) ? c with { Name = "Orders", Pinned = true } : c));
        Add(atlas, "svc-new");
        Link(atlas, "svc-new", "svc-a");

        var g = Clusters.Current(atlas);
        var orders = g.Clusters.Single(c => c.Name == "Orders");
        Assert.True(orders.Pinned);
        Assert.Equal(4, orders.Members.Count);                              // the new part is not added to a pinned cluster
        Assert.DoesNotContain(Id("svc-new"), orders.Members);
        Assert.Contains(g.Clusters, c => !c.Pinned && c.Members.Contains(Id("svc-new")));
    }

    [Fact]
    public void A_regroup_is_suggested_when_the_links_drift_and_applying_it_keeps_the_pinned_name()
    {
        var atlas = TwoGroups();
        Clusters.WritePinned(atlas, Clusters.Current(atlas).Clusters.Select(c => c with { Name = c.Members.Contains(Id("svc-a")) ? "Orders" : "Stock", Pinned = true }));
        Assert.Null(Clusters.Current(atlas).Suggestion);

        // d stops talking to the orders group and talks to all of stock instead.
        foreach (var n in "abc") atlas.Entries[Id($"svc-{n}")].Links!.RemoveAll(l => l.To == Id("svc-d"));
        foreach (var n in "fgh") Link(atlas, "svc-d", $"svc-{n}");
        var g = Clusters.Current(atlas);
        var s = Assert.IsType<Regroup>(g.Suggestion);
        Assert.Equal([(Id("svc-d"), "Orders", "Stock")], s.Moved);
        Assert.True((s.Inside - g.Inside) * 100 >= Clusters.ThresholdPoints - 1e-9);

        Clusters.WritePinned(atlas, Clusters.Afresh(atlas));
        var after = Clusters.Current(atlas);
        Assert.Null(after.Suggestion);
        Assert.Equal(["Orders", "Stock"], after.Clusters.Select(c => c.Name).Order());
        Assert.Contains(Id("svc-d"), after.Clusters.Single(c => c.Name == "Stock").Members);
        Assert.All(after.Clusters, c => Assert.True(c.Pinned));
    }

    [Fact]
    public void Pinned_parts_that_are_gone_are_dropped_and_an_empty_pin_file_is_removed()
    {
        var atlas = TwoGroups();
        Clusters.WritePinned(atlas, [new Cluster("Ghosts", true, ["repositories/gone/components/gone"])]);
        Assert.DoesNotContain(Clusters.Current(atlas).Clusters, c => c.Name == "Ghosts");
        Clusters.WritePinned(atlas, []);
        Assert.False(File.Exists(Path.Combine(atlas.Root, Clusters.FileName)));
    }

    [Theory]
    [InlineData("clusters:\n  - name: Orders\n    members:\n", "Orders has no members")]
    [InlineData("clusters:\n  - members: [a]\n", "a cluster has no name")]
    [InlineData("clusters:\n  -\n", "a cluster has no name")]
    [InlineData("clusters:\n  - name: A\n    members: [x]\n  - name: a\n    members: [y]\n", "two clusters are called a")]
    [InlineData("clusters:\n  - name: A\n    members: [x]\n  - name: B\n    members: [x]\n", "x is in both A and B")]
    public void A_broken_pin_file_is_reported(string yaml, string message)
    {
        var atlas = TwoGroups();
        File.WriteAllText(Path.Combine(atlas.Root, Clusters.FileName), yaml);
        Assert.Contains(message, Assert.Throws<UsageException>(() => Clusters.Current(atlas)).Message);
    }

    [Fact]
    public void A_pinned_name_goes_only_to_a_cluster_holding_most_of_it()
    {
        var atlas = TwoGroups();
        // A pin made of one part from each group: no fresh cluster holds more than half of it.
        Clusters.WritePinned(atlas, [new Cluster("Odd", true, [Id("svc-a"), Id("svc-e")])]);
        Assert.DoesNotContain(Clusters.Afresh(atlas), c => c.Name == "Odd");
    }

    [Fact]
    public void Pinning_one_cluster_leaves_the_other_pins_as_they_were()
    {
        var atlas = TwoGroups();
        // Orders holds a part that is missing right now, as during a rescan.
        Clusters.WritePinned(atlas, [new Cluster("Orders", true, [Id("svc-a"), Id("svc-b"), Id("svc-c"), Id("svc-d"), "repositories/away/components/away"])]);
        atlas.Save();
        var stock = Clusters.Current(atlas).Clusters.Single(c => !c.Pinned).Name;

        Assert.Equal(0, Cartographer.Cli.Cli.Discover().Run(["clusters", "pin", stock, "--atlas", atlas.Root]));
        var pins = Clusters.ReadPinned(atlas);
        Assert.Contains("repositories/away/components/away", pins.Single(p => p.Name == "Orders").Members);
        Assert.Equal(2, pins.Count);
    }

    [Fact]
    public void A_regroup_keeps_pinned_parts_that_are_missing_right_now()
    {
        var atlas = TwoGroups();
        Clusters.WritePinned(atlas, [
            new Cluster("Orders", true, [Id("svc-a"), Id("svc-b"), Id("svc-c"), Id("svc-d"), "repositories/away/components/away"]),
            new Cluster("Archive", true, ["repositories/gone/components/old"])]);
        var fresh = Clusters.Afresh(atlas);
        Assert.Contains("repositories/away/components/away", fresh.Single(c => c.Name == "Orders").Members);
        Assert.DoesNotContain(fresh, c => c.Name == "Archive");             // no cluster inherits a pin with no parts here
        Assert.DoesNotContain(fresh, c => c.Members.Contains("repositories/gone/components/old"));
    }

    [Fact]
    public void A_name_held_by_a_pin_whose_parts_are_missing_cannot_be_reused()
    {
        var atlas = TwoGroups();
        Clusters.WritePinned(atlas, [new Cluster("Archive", true, ["repositories/gone/components/old"])]);
        atlas.Save();
        var any = Clusters.Current(atlas).Clusters[0].Name;
        Assert.Equal(2, Cartographer.Cli.Cli.Discover().Run(["clusters", "rename", any, "archive", "--atlas", atlas.Root]));
        Assert.Single(Clusters.ReadPinned(atlas));
    }

    [Fact]
    public void An_empty_pin_file_means_nothing_is_pinned_and_regroup_pins_everything()
    {
        var atlas = TwoGroups();
        atlas.Save();
        File.WriteAllText(Path.Combine(atlas.Root, Clusters.FileName), "clusters:\n");
        Assert.All(Clusters.Current(atlas).Clusters, c => Assert.False(c.Pinned));
        Assert.Equal(0, Cartographer.Cli.Cli.Discover().Run(["clusters", "regroup", "--atlas", atlas.Root]));
        Assert.Equal(2, Clusters.ReadPinned(atlas).Count);
        Assert.Equal(2, Cartographer.Cli.Cli.Discover().Run(["clusters", "rename", "svc-d", "New", "Name", "--atlas", atlas.Root]));
    }

    [Fact]
    public void A_pin_whose_parts_are_all_missing_can_still_be_unpinned_and_a_switch_takes_no_value()
    {
        var atlas = TwoGroups();
        atlas.Save();
        Clusters.WritePinned(atlas, [new Cluster("Archive", true, ["repositories/gone/components/old"])]);
        var cli = Cartographer.Cli.Cli.Discover();
        Assert.Equal(2, cli.Run(["clusters", "pin", "--all", "Orders", "--atlas", atlas.Root]));
        Assert.Equal(0, cli.Run(["clusters", "unpin", "Archive", "--atlas", atlas.Root]));
        Assert.Empty(Clusters.ReadPinned(atlas));
    }

    [Fact]
    public void Parts_with_no_links_never_bring_a_regroup_suggestion()
    {
        var dir = Directory.CreateTempSubdirectory("clusters-").FullName;
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile), "organisation: Test\n");
        var atlas = Atlas.Load(dir);
        Add(atlas, "svc-a");
        Add(atlas, "svc-b");
        Clusters.WritePinned(atlas, [new Cluster("Orders", true, [Id("svc-a"), Id("svc-b")])]);
        Assert.Null(Clusters.Current(atlas).Suggestion);
    }

    [Fact]
    public void A_pinned_table_is_refused_with_its_database_named()
    {
        var atlas = TwoGroups();
        atlas.Entries["databases/stock/database"] = new Entry { Id = "databases/stock/database", Kind = Kinds.Database, Name = "Stock" };
        atlas.Entries["databases/stock/tables/dbo/items"] = new Entry { Id = "databases/stock/tables/dbo/items", Kind = Kinds.Table, Name = "dbo.items" };
        File.WriteAllText(Path.Combine(atlas.Root, Clusters.FileName), "clusters:\n  - name: A\n    members: [databases/stock/tables/dbo/items]\n");
        Assert.Contains("is not a part; use databases/stock/database", Assert.Throws<UsageException>(() => Clusters.Current(atlas)).Message);
    }
}
