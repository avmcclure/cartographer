namespace Cartographer.Tests;

public class CoreTests
{
    static Atlas NewAtlas(string config = "organisation: Test\ninternal-prefixes: [\"Acme.*\"]\n")
    {
        var dir = Directory.CreateTempSubdirectory("atlas-").FullName;
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile), config);
        return Atlas.Load(dir);
    }

    static Entry Component(string repo, string name, params Link[] links)
    {
        var e = new Entry { Id = Ids.Component(repo, name), Kind = Kinds.Component, Type = "library", Name = name, Summary = $"{name} library.", Repository = Ids.Repository(repo) };
        e.Source("proj", $"{repo}:src/{name}.csproj@abc1234");
        e.Provides = [new Provided { Name = name, As = "nuget", Basis = Basis.Extracted, Source = "proj" }];
        e.Links = links.Length > 0 ? links.ToList() : null;
        return e;
    }

    static Link DependsOn(string name, string? version = null) => new() { Type = "depends-on", Name = name, Version = version, Basis = Basis.Extracted, Source = "proj#L3" };

    [Fact]
    public void An_entry_survives_being_written_and_read_back()
    {
        var e = Component("acme-core", "Acme.Core", DependsOn("Newtonsoft.Json", "13.0.1"));
        e.Verified = new Verified { Commit = "abc1234", Date = "2026-11-21", Cartographer = "0.1.0" };
        e.Body = "## Overview\n\nText.\n\n## Open questions\n\n### q1\n\n- State: open\n- Raised by: agent\n- Attempts: 1\n- Missing: A fact.\n- Waiting on: person\n- Answer looks like: A name.\n- Answer:";
        var text = EntryFile.Write(e);
        Assert.StartsWith("---\nkind: component\nname: Acme.Core\ntype: library\n", text);
        Assert.Contains("date: \"2026-11-21\"", text);
        Assert.Equal(text, EntryFile.Write(EntryFile.Parse(e.Id, text)));
        var q = Assert.Single(QuestionList.Parse(e.Body).Items);
        Assert.Equal(("q1", "open", "agent", 1, "A fact."), (q.Id, q.State, q.RaisedBy, q.Attempts, q.Missing));
    }

    [Fact]
    public void Unknown_frontmatter_keys_are_refused()
    {
        var e = Assert.Throws<EntryFormatException>(() => EntryFile.Parse("systems/x", "---\nkind: system\nname: X\nsummary: s\nconfidence: high\n---\n"));
        Assert.Contains("confidence", e.Message);
    }

    [Fact]
    public void The_resolver_links_internal_names_lists_public_ones_and_asks_about_unknown_internal_ones()
    {
        var atlas = NewAtlas();
        var consumer = Component("acme-app", "Acme.App", DependsOn("Acme.Core", "2.0.0"), DependsOn("Newtonsoft.Json", "13.0.1"), DependsOn("Acme.Missing"));
        atlas.Entries[consumer.Id] = consumer;
        Resolver.Run(atlas);
        Assert.Equal(["Newtonsoft.Json 13.0.1"], consumer.Packages);
        Assert.Equal(2, consumer.Links!.Count(l => l.To is null));
        Assert.Contains("name Acme.Missing", consumer.Body);

        // The library is scanned later: the link resolves without rescanning the consumer.
        var core = Component("acme-core", "Acme.Core");
        atlas.Entries[core.Id] = core;
        Resolver.Run(atlas);
        Assert.Equal(core.Id, consumer.Links!.Single(l => l.Name == "Acme.Core").To);

        // A package first taken as public gains a provider: it becomes a link, and the answered question is withdrawn.
        var json = Component("acme-json", "Newtonsoft.Json");
        var missing = Component("acme-missing", "Acme.Missing");
        atlas.Entries[json.Id] = json;
        atlas.Entries[missing.Id] = missing;
        Resolver.Run(atlas);
        Assert.Null(consumer.Packages);
        Assert.Equal(3, consumer.Links!.Count(l => l.To is not null));
        Assert.Empty(QuestionList.Parse(consumer.Body).Items);
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);

        // The library goes away: the consumer is not rescanned, it gets a question.
        core.Status = "retired";
        Resolver.Run(atlas);
        Assert.Contains($"entry {core.Id}", consumer.Body);
    }

    [Fact]
    public void A_rescan_replaces_extracted_facts_and_keeps_what_agents_and_people_added()
    {
        var atlas = NewAtlas();
        Merger.Apply(atlas, "acme-app", [Component("acme-app", "Acme.App", DependsOn("Old.Package"))]);
        var e = atlas.Entries.Values.Single();
        e.Summary = "Rewritten by an agent.";
        e.Links!.Add(new Link { Type = "uses", To = "databases/sales/database", Basis = Basis.Inferred, Source = "proj" });
        e.Links.Add(new Link { Type = "depends-on", Name = "Stated.Package", Basis = Basis.Stated, Source = "person:alex@2026-11-21" });
        e.Body = "## Overview\n\nAgent prose.";

        var again = Component("acme-app", "Acme.App", DependsOn("New.Package"));
        again.Extracted = "- Target framework: net8.0";
        Merger.Apply(atlas, "acme-app", [again], [e.Id]);
        Assert.Equal("Rewritten by an agent.", e.Summary);
        Assert.Equal(["New.Package", "Stated.Package"], e.Links!.Where(l => l.Type == "depends-on").Select(l => l.Name).Order());
        Assert.Contains(e.Links!, l => l.Type == "uses");
        Assert.Contains("Agent prose.", e.Body);
        Assert.Contains("<!-- extracted -->\n- Target framework: net8.0\n<!-- /extracted -->", e.Body);

        // The component disappears from the repository: retired, not deleted.
        Merger.Apply(atlas, "acme-app", [], [e.Id]);
        Assert.Equal("retired", e.Status);
    }

    [Fact]
    public void Validation_refuses_secrets_bad_links_and_misplaced_kinds()
    {
        var atlas = NewAtlas();
        var e = Component("acme-app", "Acme.App", new Link { Type = "reads", To = "systems/billing", Basis = "certain", Source = "nowhere" });
        e.Body = "## Technical detail\n\nServer=sql01;Database=Sales;Password=hunter2secret";
        atlas.Entries[e.Id] = e;
        atlas.Entries["systems/billing"] = new Entry { Id = "systems/billing", Kind = Kinds.System, Name = "Billing", Summary = "Billing." };
        atlas.Entries["systems/wrong"] = new Entry { Id = "systems/wrong", Kind = Kinds.Table, Name = "T", Summary = "T." };
        var errors = Validator.Check(atlas).Where(i => i.Error).Select(i => i.Message).ToList();
        Assert.Contains(errors, m => m.Contains("credential"));
        Assert.Contains(errors, m => m.Contains("basis 'certain'"));
        Assert.Contains(errors, m => m.Contains("neither a key in sources"));
        Assert.Contains(errors, m => m.Contains("cannot point at a system"));
        Assert.Contains(errors, m => m.Contains("a table cannot live at this path"));
    }

    [Fact]
    public void Transports_come_from_a_fixed_list_and_roles_belong_on_components()
    {
        var atlas = NewAtlas();
        var api = Component("acme-api", "Acme.Api");
        var gateway = Component("acme-edge", "Acme.Edge",
            new Link { Type = "forwards-to", To = api.Id, How = "http", Uses = ["POST /orders"], Basis = Basis.Extracted, Source = "proj#L1" },
            new Link { Type = "reads", To = api.Id, How = "REST", Basis = Basis.Extracted, Source = "proj#L2" });
        gateway.Role = new Role { Name = "proxy", Basis = Basis.Stated, Source = "person:a@2026-11-24" };
        var db = new Entry { Id = Ids.Database("Sales"), Kind = Kinds.Database, Name = "Sales", Summary = "Sales.", Role = new Role { Name = "lookup", Basis = Basis.Stated, Source = "person:a@2026-11-24" } };
        foreach (var e in new[] { api, gateway, db }) atlas.Entries[e.Id] = e;
        var errors = Validator.Check(atlas).Where(i => i.Error).Select(i => i.Message).ToList();
        Assert.Contains(errors, m => m.Contains("how 'REST' is not one of"));
        Assert.Contains(errors, m => m.Contains("role 'proxy' is not one of"));
        Assert.Contains(errors, m => m.Contains("role belongs on components and external systems only"));
        Assert.DoesNotContain(errors, m => m.Contains("forwards-to") || m.Contains("cannot point at a component"));
    }

    [Fact]
    public void Indexes_reverse_lists_queries_and_rename_agree()
    {
        var atlas = NewAtlas("organisation: Test\nlimits: {index-rows: 2}\n");
        var core = Component("acme-core", "Acme.Core");
        var app = Component("acme-app", "Acme.App", DependsOn("Acme.Core"));
        var web = Component("acme-web", "Acme.Web", DependsOn("Acme.App"));
        foreach (var e in new[] { core, app, web }) atlas.Entries[e.Id] = e;
        atlas.Entries[Ids.Repository("acme-core")] = new Entry { Id = Ids.Repository("acme-core"), Kind = Kinds.Repository, Name = "core", Summary = "Core." };
        for (var i = 0; i < 5; i++) atlas.Entries[$"systems/s{i}"] = new Entry { Id = $"systems/s{i}", Kind = Kinds.System, Name = $"S{i}", Summary = "A system." };
        Resolver.Run(atlas);
        atlas.Save();

        var files = Indexer.Build(atlas);
        Assert.Contains("| [repositories/](repositories/index.md) | 4 |", files["index.md"]);
        Assert.Contains("index-3.md", files["systems/index.md"]);
        Assert.Contains($"- {app.Id} (extracted)", files[$"{core.Id}.used-by.md"]);
        Assert.Contains("| Acme.Core | nuget |", files["provided-names.md"]);

        Assert.StartsWith("2 entries could be affected", Queries.Impact(atlas, core.Id));
        Assert.Contains(web.Id, Queries.Impact(atlas, Ids.Repository("acme-core")));
        Assert.Contains(core.Id, Queries.Find(atlas, "acme.core"));
        Assert.Contains("depends-on:", Queries.Show(atlas, app.Id));

        Renamer.Run(atlas, Ids.Repository("acme-core"), Ids.Repository("platform-core"));
        var reloaded = Atlas.Load(atlas.Root);
        Assert.Equal("repositories/platform-core/components/acme.core", reloaded.Entries[app.Id].Links!.Single().To);
        Assert.StartsWith("platform-core:", reloaded.Entries["repositories/platform-core/components/acme.core"].Sources!["proj"]);
        Assert.DoesNotContain(Validator.Check(reloaded), i => i.Error);
    }

    [Fact]
    public void Any_repository_gets_an_entry_from_the_generic_pass()
    {
        var atlas = NewAtlas();
        var repo = Directory.CreateTempSubdirectory("repo-").FullName;
        File.WriteAllText(Path.Combine(repo, "app.py"), "print('hi')\n");
        File.WriteAllText(Path.Combine(repo, "requirements.txt"), "flask\n");
        Scanner.Run(atlas, Checkout.Open("tools-scripts", repo), extractors: [new GenericExtractor()]);
        var e = atlas.Entries[Ids.Repository("tools-scripts")];
        Assert.Equal(["python"], e.Stack);
        Assert.Contains("`requirements.txt`", e.Body);
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
    }

    [Fact]
    public void A_live_read_that_disagrees_with_the_repository_is_recorded_as_drift_and_clears_when_it_agrees()
    {
        var atlas = NewAtlas();
        Entry Orders(string origin, string pointer, string amountType)
        {
            var t = new Entry { Id = Ids.DbObject("Shop", "tables", "dbo", "Orders"), Kind = Kinds.Table, Name = "dbo.Orders", Summary = "Orders.", Database = Ids.Database("Shop") };
            t.Source(origin, pointer);
            t.Extracted = DbText.Columns([new("Id", "int", false, "PK"), new("Amount", amountType, false)]);
            return t;
        }
        Merger.Apply(atlas, "shop-db", [Orders("schema", "shop-db:Tables/Orders.sql@abc1234", "decimal(18,2)")]);
        Merger.Apply(atlas, "instance:sql-test-01", [Orders("live-sql-test-01", "instance:sql-test-01@2026-01-01", "money")]);
        var e = atlas.Entries.Values.Single();
        var drift = Assert.Single(e.Drift!);
        Assert.Equal("column Amount is decimal(18,2) not null in the repository and money not null on sql-test-01", drift.What);
        Assert.Equal(["shop-db", "instance:sql-test-01"], drift.Sources);
        Assert.Contains("decimal(18,2)", e.Body);

        Merger.Apply(atlas, "instance:sql-test-01", [Orders("live-sql-test-01", "instance:sql-test-01@2026-01-02", "decimal(18,2)")]);
        Assert.Empty(e.Drift!);
    }
}
