using System.Text.Json;
using Cartographer.Cli;

namespace Cartographer.Tests;

/// <summary>Site data, from the hand-written atlas in fixtures/sample-atlas.</summary>
public class SiteTests
{
    static string Sample => Path.Combine(Fixtures.Root(), "sample-atlas");

    /// <summary>A copy of the sample atlas that a test may change.</summary>
    static Atlas Copy()
    {
        var root = Directory.CreateTempSubdirectory("sample-").FullName;
        foreach (var file in Directory.EnumerateFiles(Sample, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(root, Path.GetRelativePath(Sample, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
        return Atlas.Load(root);
    }

    static string Out() => Directory.CreateTempSubdirectory("site-data-").FullName;

    static SortedDictionary<string, string> Files(string dir) => new(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
        .ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), File.ReadAllText), StringComparer.Ordinal);

    static JsonElement Json(string dir, string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, file))).RootElement;

    static Entry Component(string id, string name, params Link[] links)
    {
        var e = new Entry
        {
            Id = id, Kind = Kinds.Component, Type = "service", Name = name, Summary = "Added by a test.", Repository = Ids.Area(id) + "/repository",
            Systems = [new Membership { Id = "systems/ordering", Basis = Basis.Stated, Source = "person:test@2026-11-20" }],
            Links = [.. links],
        };
        return e;
    }

    [Fact]
    public void The_sample_atlas_is_valid_and_written_the_way_the_CLI_writes_entries()
    {
        var atlas = Atlas.Load(Sample);
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
        Assert.True(atlas.Entries.Count >= 35);
        foreach (var e in atlas.Entries.Values)
        {
            var (path, canonical) = (atlas.PathOf(e.Id), EntryFile.Write(e));
            if (Environment.GetEnvironmentVariable("CARTOGRAPHER_ACCEPT") == "1") File.WriteAllText(path, canonical);
            Assert.True(File.ReadAllText(path) == canonical, $"{e.Id} is not in the form the CLI writes. Run CARTOGRAPHER_ACCEPT=1 dotnet test once.");
        }
        Assert.Equal(Indexer.Build(atlas), Files(Path.Combine(Sample, Indexer.Folder)));
    }

    [Fact]
    public void Prepare_is_deterministic_and_complete()
    {
        var atlas = Copy();
        var (first, second) = (Out(), Out());
        SitePrepare.Run(atlas, first);
        SitePrepare.Run(Atlas.Load(atlas.Root), second);
        Assert.Equal(Files(first), Files(second));
        Assert.False(File.Exists(Path.Combine(atlas.Root, Clusters.FileName)));   // prepare never pins

        foreach (var id in atlas.Entries.Keys) Assert.True(File.Exists(Path.Combine(first, "entries", id + ".json")), $"no entry file for {id}");
        Assert.Equal(atlas.Entries.Count, Json(first, "everything.json").GetArrayLength());
        Assert.Equal(2, Json(first, "changes.json").GetArrayLength());
        Assert.Contains(Json(first, "questions.json").EnumerateArray(), q => q.GetProperty("state").GetString() == "needs-person");

        // Evidence becomes a link to the file at the repository's primary location, on either host.
        var orders = File.ReadAllText(Path.Combine(first, "entries/databases/sales/tables/dbo/orders.json"));
        Assert.Contains("https://github.com/brackley/sales-order-service/blob/4c1e9a2/src/Brackley.Orders.Api/Data/OrderRepository.cs#L61-L79", orders);
        Assert.Contains("https://dev.azure.com/brackley/Sales/_git/storefront?path=/db/003_orders.sql&version=GC9be0713&line=41", orders);
    }

    [Fact]
    public void Every_part_is_in_one_cluster_and_every_link_between_parts_is_counted_once()
    {
        var atlas = Atlas.Load(Sample);
        var dir = Out();
        SitePrepare.Run(atlas, dir);
        var map = Json(dir, "clusters.json");
        var members = map.GetProperty("clusters").EnumerateArray().SelectMany(c => c.GetProperty("members").EnumerateArray().Select(m => (Part: m.GetProperty("id").GetString()!, Key: c.GetProperty("key").GetString()!))).ToList();
        var (parts, graph) = Clusters.Graph(atlas);
        Assert.Equal(parts.Order(StringComparer.Ordinal), members.Select(m => m.Part).Order(StringComparer.Ordinal));

        // A table's link counts towards its database, and the entry says which part and cluster it belongs to.
        Assert.Equal(graph.Count, map.GetProperty("parts").EnumerateArray().Sum(l => l.GetProperty("count").GetInt32()));
        var table = Json(dir, "entries/databases/sales/tables/dbo/orders.json");
        Assert.Equal("databases/sales/database", table.GetProperty("part").GetProperty("id").GetString());
        Assert.Equal(members.Single(m => m.Part == "databases/sales/database").Key, table.GetProperty("cluster").GetString());
        // Data read from the database flows out of it to the reader; calls keep their own direction and are marked.
        bool Call(JsonElement l) => l.TryGetProperty("call", out var c) && c.GetBoolean();
        var links = map.GetProperty("parts").EnumerateArray().ToList();
        var reads = links.Single(l => l.GetProperty("from").GetString() == "databases/sales/database" && l.GetProperty("to").GetString() == "repositories/sales-storefront/components/webshop" && !Call(l));
        Assert.Contains(links, l => l.GetProperty("from").GetString() == "repositories/sales-storefront/components/webshop" && l.GetProperty("to").GetString() == "databases/sales/database" && !Call(l));   // it writes too
        Assert.DoesNotContain(links, l => l.GetProperty("from").GetString() == "repositories/sales-storefront/components/webshop" && l.EnumerateObject().Any(p => p.Name == "verbs" && p.Value.EnumerateArray().Any(v => v.GetProperty("verb").GetString() == "depends on")));
        var verbs = reads.GetProperty("verbs").EnumerateArray().Select(v => (Verb: v.GetProperty("verb").GetString(), Count: v.GetProperty("count").GetInt32())).ToList();
        Assert.Contains(verbs, v => v.Verb == "reads" && v.Count > 0);
        Assert.Equal(verbs.OrderByDescending(v => v.Count).Select(v => v.Count), verbs.Select(v => v.Count));   // most common first
    }

    [Fact]
    public void Pass_throughs_and_lookups_get_their_role_and_links_carry_transport_and_operations()
    {
        var atlas = Copy();
        var dir = Out();
        SitePrepare.Run(atlas, dir);
        var map = Json(dir, "clusters.json");
        string? Role(JsonElement map, string id) => map.GetProperty("clusters").EnumerateArray().SelectMany(c => c.GetProperty("members").EnumerateArray())
            .Single(m => m.GetProperty("id").GetString() == id).TryGetProperty("role", out var r) ? r.GetString() : null;
        const string gateway = "repositories/sales-edge/components/order-gateway", lookup = "repositories/sales-edge/components/customer-lookup", api = "repositories/sales-order-service/components/order-api";
        Assert.Equal("pass-through", Role(map, gateway));
        Assert.Equal("lookup", Role(map, lookup));   // read by the shop and the order API, fed only by its database
        Assert.Null(Role(map, api));

        // A forward carries data on, like a write; transport and operations ride along.
        var forward = map.GetProperty("parts").EnumerateArray().Single(l => l.GetProperty("from").GetString() == gateway && l.GetProperty("to").GetString() == api);
        Assert.False(forward.TryGetProperty("call", out _));
        Assert.Equal("http", forward.GetProperty("over")[0].GetProperty("how").GetString());
        Assert.Equal(["GET /orders/{id}", "POST /orders"], forward.GetProperty("ops").EnumerateArray().Select(o => o.GetString()));

        // Reading through a pass-through brings data back from what it forwards to.
        bool Between(JsonElement map, string from, string to) => map.GetProperty("parts").EnumerateArray().Any(l => l.GetProperty("from").GetString() == from && l.GetProperty("to").GetString() == to && !l.TryGetProperty("call", out _));
        Assert.False(Between(map, api, gateway));
        var reader = Component("repositories/sales-edge/components/order-status", "OrderStatus", new Link { Type = "reads", To = gateway, How = "http", Basis = Basis.Stated, Source = "person:test@2026-11-20" });
        atlas.Entries[reader.Id] = reader;
        SitePrepare.Run(atlas, dir);
        map = Json(dir, "clusters.json");
        Assert.True(Between(map, api, gateway) && Between(map, gateway, reader.Id) && Between(map, gateway, api));   // the shop still writes through it
        atlas.Entries.Remove(reader.Id);

        // Through a chain of pass-throughs too: a read at the outer one comes back along every hop.
        var outer = Component("repositories/sales-edge/components/edge", "Edge", new Link { Type = "forwards-to", To = gateway, How = "http", Basis = Basis.Stated, Source = "person:test@2026-11-20" });
        var far = Component("repositories/sales-edge/components/far-reader", "FarReader", new Link { Type = "reads", To = outer.Id, How = "http", Basis = Basis.Stated, Source = "person:test@2026-11-20" });
        atlas.Entries[outer.Id] = outer;
        atlas.Entries[far.Id] = far;
        SitePrepare.Run(atlas, dir);
        map = Json(dir, "clusters.json");
        Assert.True(Between(map, api, gateway) && Between(map, gateway, outer.Id) && Between(map, outer.Id, far.Id));
        atlas.Entries.Remove(outer.Id);
        atlas.Entries.Remove(far.Id);

        // Anything written into a lookup makes it part of the flow again; a stated role wins over what the links say.
        var writer = Component("repositories/sales-edge/components/customer-admin", "CustomerAdmin", new Link { Type = "writes", To = lookup, How = "http", Basis = Basis.Stated, Source = "person:test@2026-11-20" });
        atlas.Entries[writer.Id] = writer;
        atlas.Entries[api].Role = new Role { Name = "lookup", Basis = Basis.Stated, Source = "person:test@2026-11-20" };
        SitePrepare.Run(atlas, dir);
        map = Json(dir, "clusters.json");
        Assert.Null(Role(map, lookup));
        Assert.Equal("lookup", Role(map, api));
        Assert.Empty(Validator.Check(atlas).Where(i => i.Error && i.Id == api));
    }

    [Fact]
    public void Pinned_names_and_a_regroup_suggestion_reach_the_site()
    {
        var atlas = Copy();
        var first = Clusters.Current(atlas).Clusters;
        Clusters.WritePinned(atlas, first.Select((c, i) => c with { Name = $"Pinned {i}", Pinned = true }));
        var dir = Out();
        Assert.Equal(0, SiteCommands.Prepare(Atlas.Load(atlas.Root), dir, null));
        var map = Json(dir, "clusters.json");
        Assert.All(map.GetProperty("clusters").EnumerateArray(), c => Assert.True(c.GetProperty("pinned").GetBoolean()));
        Assert.Contains(map.GetProperty("clusters").EnumerateArray(), c => c.GetProperty("key").GetString() == "pinned-0");
        Assert.Equal(JsonValueKind.Null, map.TryGetProperty("suggestion", out var none) ? none.ValueKind : JsonValueKind.Null);
    }
}
