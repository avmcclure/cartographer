using System.Text.Json;
using Cartographer.Cli;

namespace Cartographer.Tests;

/// <summary>Site data and plan layouts, from the hand-written atlas in fixtures/sample-atlas.</summary>
public class SiteTests
{
    static string Sample => Path.Combine(Fixtures.Root(), "sample-atlas");

    /// <summary>A copy of the sample atlas that a test may change, with the layouts it was committed with.</summary>
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

    /// <summary>Plot ID to its corner, for every sheet.</summary>
    static Dictionary<string, (int X, int Y)> Corners(string dir) => Files(dir).Where(f => f.Key.StartsWith("sheets/"))
        .SelectMany(f => JsonDocument.Parse(f.Value).RootElement.GetProperty("plots").EnumerateArray()
            .Select(p => (Key: $"{f.Key} {p.GetProperty("id").GetString()}", At: (p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32()))))
        .ToDictionary(p => p.Key, p => p.At);

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
    public void Prepare_is_deterministic_and_complete_and_leaves_committed_layouts_alone()
    {
        var atlas = Copy();
        var layouts = Files(Path.Combine(atlas.Root, "layouts"));
        var (first, second) = (Out(), Out());
        Assert.All(SitePrepare.Run(atlas, first), s => Assert.True(s.Reason is null && !s.Written, $"{s.Sheet} was laid out again"));
        SitePrepare.Run(Atlas.Load(atlas.Root), second);
        Assert.Equal(Files(first), Files(second));
        Assert.Equal(layouts, Files(Path.Combine(atlas.Root, "layouts")));

        foreach (var id in atlas.Entries.Keys) Assert.True(File.Exists(Path.Combine(first, "entries", id + ".json")), $"no entry file for {id}");
        Assert.Equal(atlas.Entries.Count, Json(first, "gazetteer.json").GetArrayLength());
        Assert.Equal(2, Json(first, "changes.json").GetArrayLength());
        Assert.Contains(Json(first, "questions.json").EnumerateArray(), q => q.GetProperty("state").GetString() == "needs-person");

        // Every link whose target has an entry is in the links schedule of at least one sheet.
        var scheduled = Files(first).Where(f => f.Key.StartsWith("sheets/")).SelectMany(f => JsonDocument.Parse(f.Value).RootElement.GetProperty("links").EnumerateArray())
            .Select(l => $"{l.GetProperty("from").GetString()} {l.GetProperty("verb").GetString()} {l.GetProperty("to").GetString()}").ToHashSet();
        var links = atlas.Entries.Values.SelectMany(e => (e.Links ?? []).Where(l => l.To is not null).Select(l => $"{e.Id} {Sheets.Verb(l.Type)} {l.To}")).ToList();
        Assert.True(links.Count > 50);
        Assert.All(links, l => Assert.Contains(l, scheduled));

        // Evidence becomes a link to the file at the repository's primary location, on either host.
        var orders = File.ReadAllText(Path.Combine(first, "entries/databases/sales/tables/dbo/orders.json"));
        Assert.Contains("https://github.com/brackley/sales-order-service/blob/4c1e9a2/src/Brackley.Orders.Api/Data/OrderRepository.cs#L61-L79", orders);
        Assert.Contains("https://dev.azure.com/brackley/Sales/_git/storefront?path=/db/003_orders.sql&version=GC9be0713&line=41", orders);
    }

    [Fact]
    public void Adding_an_entry_moves_no_plot_that_was_already_placed()
    {
        var atlas = Copy();
        var before = Out();
        SitePrepare.Run(atlas, before);

        var added = Component("repositories/sales-order-service/components/order-export", "Brackley.Orders.Export",
            new Link { Type = "reads", To = "databases/sales/tables/dbo/orders", Basis = Basis.Inferred, Source = "person:test@2026-11-20" });
        atlas.Entries[added.Id] = added;
        atlas.Save();
        var after = Out();
        Assert.All(SitePrepare.Run(Atlas.Load(atlas.Root), after), s => Assert.Null(s.Reason));

        var (was, now) = (Corners(before), Corners(after));
        Assert.All(was, p => Assert.Equal(p.Value, now[p.Key]));
        Assert.Contains($"sheets/system/ordering.json {added.Id}", now.Keys);
        Assert.Contains($"sheets/area/repositories/sales-order-service.json {added.Id}", now.Keys);
        Assert.Contains($"sheets/area/databases/sales.json repositories/sales-order-service/repository", now.Keys);
    }

    [Fact]
    public void A_sheet_that_needs_a_resurvey_is_reported_and_its_layout_left_as_it_was()
    {
        var atlas = Copy();
        var layout = Surveyor.PathOf(atlas, "system/ordering");
        var stored = File.ReadAllText(layout);

        // A plot that grows into its neighbour cannot be kept where it is without moving something.
        atlas.Entries["repositories/sales-storefront/components/webshop"].Name = "WebShop with a name far too long for its plot";
        atlas.Save();
        var dir = Out();
        var results = SitePrepare.Run(Atlas.Load(atlas.Root), dir);

        var ordering = results.Single(s => s.Sheet == "system/ordering");
        Assert.NotNull(ordering.Reason);
        Assert.False(ordering.Written);
        Assert.Equal(stored, File.ReadAllText(layout));
        Assert.True(Json(dir, "sheets/system/ordering.json").TryGetProperty("unplaced", out _));
        Assert.True(Json(dir, "sheets/system/ordering.json").GetProperty("plots").GetArrayLength() > 0);
        Assert.Contains(results, s => s.Sheet == "system/billing" && s.Reason is null);
        Assert.True(File.Exists(Path.Combine(dir, "sheets/system/billing.json")));
        Assert.Equal(1, SiteCommands.Prepare(Atlas.Load(atlas.Root), dir, null));

        // The deliberate re-survey lays the sheet out afresh, and prepare is then content again.
        var fresh = SitePrepare.Resurvey(Atlas.Load(atlas.Root), "system/ordering");
        Assert.True(fresh.Written && fresh.Reason is null);
        Assert.NotEqual(stored, File.ReadAllText(layout));
        Assert.Equal(0, SiteCommands.Prepare(Atlas.Load(atlas.Root), dir, null));
    }

    [Fact]
    public void Summarised_lines_carry_exact_totals()
    {
        var w = new World(Atlas.Load(Sample));
        var sheets = Sheets.All(w).ToDictionary(s => s.Id);
        foreach (var sheet in sheets.Values)
            foreach (var line in sheet.Lines)
            {
                Assert.Equal(line.Total, line.Verbs.Sum(v => v.Count));
                if (sheet.Level != "organisation") Assert.Equal(line.Total, sheet.Links.Count(l => l.Line == line.Id));
            }

        // Counted by hand from the entries: what the storefront's two programs do to the Sales database, and what Billing takes from Ordering.
        var storefront = sheets["system/ordering"].Lines.Single(l => l.Id == "repositories/sales-storefront/repository>databases/sales/database");
        Assert.Equal("reads 5, writes 3, uses 2, calls 1, defines 1", storefront.Text);
        Assert.Equal(Basis.Inferred, storefront.Basis);
        var billing = sheets["organisation"].Lines.Single(l => l.Id == "systems/billing>systems/ordering");
        Assert.Equal((4, "4"), (billing.Total, billing.Text));
        Assert.Equal(Basis.Extracted, billing.Basis);

        // Every link between two different plots of the organisation sheet is counted on exactly one line.
        string Home(string id) => w.Entry(id).Kind is Kinds.Channel or Kinds.External ? id : w.GroupsOf(id).Single();
        Assert.Equal(w.Edges.Count(e => Home(e.From) != Home(e.To)), sheets["organisation"].Lines.Sum(l => l.Total));

        var stated = sheets["system/warehouse"].Lines.Single(l => l.Beyond == "Ordering" && l.Text.Contains("calls"));
        Assert.True(stated.Stated);
    }

    [Fact]
    public void Until_a_system_exists_repositories_are_grouped_by_folder_prefix()
    {
        var atlas = Copy();
        foreach (var system in atlas.OfKind(Kinds.System).ToList()) atlas.Entries.Remove(system.Id);
        var w = new World(atlas);
        Assert.Equal(["provisional/finance", "provisional/platform", "provisional/sales", "provisional/warehouse", World.Unassigned], w.Groups.Select(g => g.Key));
        Assert.Equal(["databases/ledger/database", "databases/sales/database"], w.GroupByKey(World.Unassigned).Members);
        Assert.Contains(Sheets.All(w), s => s.Id == "system/sales" && s.Plots.Any(p => p.Id == "repositories/sales-storefront/components/webshop"));
    }

    [Fact]
    public void Grid_references_come_from_position()
    {
        Assert.Equal("A1", Surveyor.Reference(0, 0));
        Assert.Equal("B3", Surveyor.Reference(Sheets.Cell, 2 * Sheets.Cell + 5));
        Assert.Equal("AA1", Surveyor.Reference(26 * Sheets.Cell, 0));
    }
}
