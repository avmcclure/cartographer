using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Cartographer.Tests;

public class LiveDbTests
{
    const string Shop = "one-schema-three-forms";
    static readonly InstanceConfig Test01 = new() { Name = "sql-test-01", Server = "sql-test-01.acme.local", Environment = "test" };
    static readonly DateTime Modified = new(2026, 1, 1);

    /// <summary>The Shop database of the fixture as a server's catalog would show it: the rows the catalog queries return.</summary>
    static DbCatalog ShopCatalog(int emailLength = 256)
    {
        var c = new DbCatalog { Database = "Shop" };
        // "!" is NOT NULL, "PK" the primary key.
        void Table(string name, params string[] columns)
        {
            c.Objects.Add(new("dbo", name, "U", Modified));
            foreach (var column in columns)
            {
                var p = column.Split(' ');
                c.Columns.Add(new("dbo", name, p[0], p[1].TrimEnd('!'), !p[1].EndsWith('!'), PrimaryKey: p.Length > 2));
            }
        }
        void Module(string type, string name, string folder)
        {
            c.Objects.Add(new("dbo", name, type, Modified));
            c.Modules.Add(new("dbo", name, File.ReadAllText(Path.Combine(Fixtures.Root(), Shop, "acme-shop-database", "Shop", "dbo", folder, name + ".sql"))));
        }
        Table("Customers", "Id int! PK", "Name nvarchar(100)!", $"Email nvarchar({emailLength})");
        Table("Products", "Id int! PK", "Sku varchar(20)!", "Name nvarchar(100)!", "Price decimal(10,2)!");
        Table("Orders", "Id int! PK", "CustomerId int!", "PlacedAt datetime2(0)!", "Status varchar(20)!");
        Table("OrderLines", "Id int! PK", "OrderId int!", "ProductId int!", "Quantity int!", "UnitPrice decimal(10,2)!");
        Table("OrderAudit", "Id bigint! PK", "OrderId int!", "Status varchar(20)!", "ChangedAt datetime2(0)!");
        c.ForeignKeys.Add(new("dbo", "Orders", "FK_Orders_Customers", false, "CustomerId", "dbo", "Customers"));
        c.ForeignKeys.Add(new("dbo", "OrderLines", "FK_OrderLines_Orders", false, "OrderId", "dbo", "Orders"));
        c.ForeignKeys.Add(new("dbo", "OrderLines", "FK_OrderLines_Products", false, "ProductId", "dbo", "Products"));
        Module("FN", "OrderTotal", "Functions");
        Module("V", "OrderSummary", "Views");
        Module("P", "PlaceOrder", "Stored Procedures");
        Module("P", "CompleteOrder", "Stored Procedures");
        Module("TR", "trg_Orders_Audit", "Triggers");
        c.Triggers.Add(new("dbo", "trg_Orders_Audit", "Orders", false, "insert"));
        c.Triggers.Add(new("dbo", "trg_Orders_Audit", "Orders", false, "update"));
        c.Parameters.Add(new("dbo", "OrderTotal", "@OrderId", "int"));
        c.Parameters.Add(new("dbo", "PlaceOrder", "@CustomerId", "int"));
        c.Parameters.Add(new("dbo", "PlaceOrder", "@OrderId", "int", true));
        c.Parameters.Add(new("dbo", "CompleteOrder", "@OrderId", "int"));
        return c;
    }

    static Atlas WithInstance(Atlas atlas, string instances = "  - {name: sql-test-01, server: sql-test-01.acme.local, environment: test, databases: [Shop, Ledger]}\n")
    {
        File.AppendAllText(Path.Combine(atlas.Root, Atlas.ConfigFile), "instances:\n" + instances);
        return Atlas.Load(atlas.Root);
    }

    static Atlas EmptyAtlas()
    {
        var dir = Directory.CreateTempSubdirectory("atlas-").FullName;
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile), "organisation: Test\n");
        return Atlas.Load(dir);
    }

    static string Block(Entry e) => Regex.Match(e.Extracted ?? e.Body, @"(?:<!-- extracted -->\n)?(.*?)(?:<!-- /extracted -->|$)", RegexOptions.Singleline).Groups[1].Value.Trim('\n');

    [Fact]
    public void Catalog_rows_become_entries_citing_the_instance()
    {
        var c = new DbCatalog { Database = "Sales" };
        c.Objects.AddRange([new("crm", "Client Notes", "U", Modified), new("dbo", "Clients", "U", Modified), new("crm", "ClientNames", "V", Modified),
            new("crm", "Touch", "P", Modified), new("crm", "trg_Notes", "TR", Modified)]);
        c.Columns.AddRange([new("dbo", "Clients", "Id", "int", false, PrimaryKey: true), new("crm", "Client Notes", "ClientId", "int", false), new("crm", "Client Notes", "Body", LiveDb.TypeName("nvarchar", -1, 0, 0), true),
            new("crm", "Client Notes", "Length", "int", false, Computed: true)]);
        c.ForeignKeys.Add(new("crm", "Client Notes", "FK__Client__1A2B", true, "ClientId", "dbo", "Clients"));
        c.Triggers.Add(new("crm", "trg_Notes", "Client Notes", true, "delete"));
        c.Modules.AddRange([
            new("crm", "ClientNames", "CREATE VIEW crm.OldName AS SELECT Id FROM dbo.Clients"),
            new("crm", "Touch", "CREATE PROCEDURE crm.Touch @Id int, @Rows int OUTPUT AS UPDATE dbo.Clients SET Id = Id WHERE Id = @Id; EXEC Audit.dbo.Log @Id; EXEC(@sql)"),
            new("crm", "trg_Notes", null, Encrypted: true)]);
        c.Parameters.AddRange([new("crm", "Touch", "@Id", "int"), new("crm", "Touch", "@Rows", "int", true)]);

        var entries = LiveDb.Entries(c, Test01).ToDictionary(e => e.Id);
        Assert.Equal(["databases/sales/database", "databases/sales/routines/crm/touch", "databases/sales/tables/crm/client-notes", "databases/sales/tables/crm/clientnames",
            "databases/sales/tables/dbo/clients", "databases/sales/triggers/crm/trg_notes"], entries.Keys.Order(StringComparer.Ordinal));
        Assert.All(entries.Values, e =>
        {
            Assert.Equal("instance:sql-test-01@" + Fixtures.Date, Assert.Single(e.Sources!).Value);
            Assert.All(e.Links ?? [], l => Assert.Equal(("live-sql-test-01", Basis.Extracted), (l.Source, l.Basis)));
            Assert.Null(e.Verified!.Commit);
        });

        var db = entries["databases/sales/database"];
        Assert.Equal(("Sales", "sqlserver", "Database Sales, found on instance sql-test-01; not yet described."), (db.Name, db.Engine, db.Summary));
        var instance = Assert.Single(db.Instances!);
        Assert.Equal(("sql-test-01", "test", Fixtures.Date, "full"), (instance.Name, instance.Environment, instance.Read, instance.Permission));
        Assert.Equal("- Tables: 2\n- Views: 1\n- Procedures: 1\n- Functions: 0\n- Triggers: 1\n- Dynamic SQL statements, not followed: 1", Block(db));

        var notes = entries["databases/sales/tables/crm/client-notes"];
        Assert.Equal("crm.Client Notes", notes.Name);
        Assert.Equal("| Column | Type | Nullable | Key |\n|---|---|---|---|\n| ClientId | int | no | FK |\n| Body | nvarchar(max) | yes |  |\n| Length | computed | yes |  |", Block(notes));
        // A generated constraint name is not a name: the repository form of the same key has none either.
        var fk = Assert.Single(notes.Links!);
        Assert.Equal(("references", "databases/sales/tables/dbo/clients", null), (fk.Type, fk.To, fk.Name));

        // The catalog's name wins over the one in the definition, which a rename leaves stale.
        Assert.Equal("crm.ClientNames", entries["databases/sales/tables/crm/clientnames"].Name);

        var touch = entries["databases/sales/routines/crm/touch"];
        Assert.Equal("procedure", touch.Type);
        Assert.StartsWith("| Parameter | Type | Direction |\n|---|---|---|\n| @Id | int | in |\n| @Rows | int | out |\n\nDefinition read from instance sql-test-01.\nRuns dynamic SQL in 1 place", Block(touch));
        Assert.Equal([("writes", "databases/sales/tables/dbo/clients", null, null), ("calls", null, "dbo.Log", "Audit")], touch.Links!.Select(l => (l.Type, l.To, l.Name, l.Database)));

        var trigger = entries["databases/sales/triggers/crm/trg_notes"];
        Assert.Equal("Trigger crm.trg_Notes on crm.Client Notes in Sales; not yet described.", trigger.Summary);
        Assert.StartsWith("Fires instead of delete on crm.Client Notes.\n\nIts definition could not be read on instance sql-test-01", Block(trigger));
        Assert.Equal("databases/sales/tables/crm/client-notes", Assert.Single(trigger.Links!).To);
    }

    [Theory]
    [InlineData("varchar", 20, 0, 0, "varchar(20)")]
    [InlineData("nvarchar", 512, 0, 0, "nvarchar(256)")]
    [InlineData("varbinary", -1, 0, 0, "varbinary(max)")]
    [InlineData("decimal", 9, 18, 2, "decimal(18,2)")]
    [InlineData("datetime2", 6, 19, 0, "datetime2(0)")]
    [InlineData("int", 4, 10, 0, "int")]
    [InlineData("dbo.PhoneNumber", 40, 0, 0, "dbo.phonenumber")]
    public void Catalog_types_are_worded_as_the_repository_parser_words_them(string type, int maxLength, int precision, int scale, string expected) =>
        Assert.Equal(expected, LiveDb.TypeName(type, maxLength, precision, scale));

    [Fact]
    public void The_fixture_schema_read_live_gives_the_entries_the_repository_extractor_gives()
    {
        var x = new Extraction(Fixtures.Checkout(Shop, "acme-shop-database"));
        new SqlSchemaExtractor().Extract(x);
        var fromRepository = x.Entries.Where(e => e.Id.StartsWith("databases/shop/") && e.Kind != Kinds.Database).ToDictionary(e => e.Id);
        var live = LiveDb.Entries(ShopCatalog(), Test01).Where(e => e.Kind != Kinds.Database).ToDictionary(e => e.Id);

        Assert.Equal(10, live.Count);
        Assert.Equal(fromRepository.Keys.Order(StringComparer.Ordinal), live.Keys.Order(StringComparer.Ordinal));
        // Where a module is defined is the one sentence that differs: a file and lines, or the instance.
        static string Comparable(Entry e) => Regex.Replace(Block(e), @"^(Defined in|Definition read from) .*$", "", RegexOptions.Multiline);
        static IEnumerable<string> Links(Entry e) => (e.Links ?? []).Select(l => $"{l.Type}|{l.To}|{l.Name}|{l.Database}|{l.How}|{l.Basis}").Order(StringComparer.Ordinal);
        foreach (var (id, r) in fromRepository)
        {
            var l = live[id];
            Assert.Equal((r.Kind, r.Name, r.Type, r.Summary, r.Database), (l.Kind, l.Name, l.Type, l.Summary, l.Database));
            Assert.Equal(Links(r), Links(l));
            Assert.Equal(Comparable(r), Comparable(l));
        }
    }

    [Fact]
    public void Every_read_records_the_permission_it_had_and_says_what_it_could_not_see()
    {
        var c = ShopCatalog();
        Assert.Equal(("full", ""), (LiveDb.Permission(c), LiveDb.NotRead(c)));

        c.Dependencies = false;
        Assert.Equal("definitions", LiveDb.Permission(c));

        // Missing VIEW DEFINITION shows as NULL definitions, not as an error.
        c.Modules[0] = c.Modules[0] with { Definition = null };
        Assert.Equal("limited", LiveDb.Permission(c));
        Assert.Contains("the definitions of 1 of 5 views, routines and triggers", LiveDb.NotRead(c));

        // An encrypted definition is NULL for everyone: not a sign of limited permission.
        c.Modules[0] = c.Modules[0] with { Encrypted = true };
        Assert.Equal("definitions", LiveDb.Permission(c));
        Assert.Contains("1 encrypted definition", LiveDb.NotRead(c));

        c.ViewDefinition = false;
        var atlas = WithInstance(EmptyAtlas());
        LiveDb.Apply(atlas, Test01, c);
        var db = atlas.Entries["databases/shop/database"];
        Assert.Equal("limited", db.Instances!.Single().Permission);
        Assert.Contains("- Live read of sql-test-01: permission limited. Not read: the identity lacks VIEW DEFINITION on the database, so objects it has no permission on are not listed and absence here does not mean none exist;", db.Body);
    }

    [Fact]
    public void A_live_read_merges_into_the_repository_entries_and_records_column_and_existence_drift()
    {
        var atlas = WithInstance(Fixtures.Scan(Shop));
        var c = ShopCatalog();
        // The instance lacks the audit table and its trigger, and has a procedure and 25 tables no repository defines.
        c.Objects.RemoveAll(o => o.Name is "OrderAudit" or "trg_Orders_Audit");
        c.Objects.Add(new("dbo", "PurgeOrders", "P", Modified));
        c.Modules.Add(new("dbo", "PurgeOrders", "CREATE PROCEDURE dbo.PurgeOrders AS DELETE FROM dbo.Orders"));
        for (var i = 1; i <= 25; i++) c.Objects.Add(new("dbo", $"Import{i:00}", "U", Modified));
        LiveDb.Apply(atlas, Test01, c);
        Resolver.Run(atlas);
        atlas.Save();

        // The same objects, not new ones: every repository entry of Shop is still the only entry for its object.
        var customers = atlas.Entries["databases/shop/tables/dbo/customers"];
        Assert.Equal(["acme-shop-database", "acme-shop-migrations", "instance:sql-test-01"], customers.Sources!.Values.Select(Evidence.OriginOf).Order(StringComparer.Ordinal));
        var column = Assert.Single(customers.Drift!);
        Assert.Equal("column Email is nvarchar(200) null in the repository and nvarchar(256) null on sql-test-01", column.What);
        Assert.Null(atlas.Entries["databases/shop/tables/dbo/orders"].Drift);

        var db = atlas.Entries["databases/shop/database"];
        var drift = Assert.Single(db.Drift!);
        Assert.Equal(
            "defined in the repository but not found on sql-test-01: table dbo.OrderAudit, trigger dbo.trg_Orders_Audit; " +
            "found on sql-test-01 but not defined in the repository: routine dbo.PurgeOrders, " + string.Join(", ", Enumerable.Range(1, 19).Select(i => $"table dbo.Import{i:00}")) + " and 6 more",
            drift.What);
        Assert.Equal(["acme-shop-database", "acme-shop-migrations", "instance:sql-test-01"], drift.Sources);
        Assert.Equal(("sql-test-01", "full"), (db.Instances!.Single().Name, db.Instances!.Single().Permission));
        Assert.Empty(QuestionList.Parse(db.Body).Items);

        // An object found only live is still an entry, with the instance as its only source.
        var purge = atlas.Entries["databases/shop/routines/dbo/purgeorders"];
        Assert.Equal("instance:sql-test-01@" + Fixtures.Date, Assert.Single(purge.Sources!).Value);
        Assert.Equal("databases/shop/tables/dbo/orders", Assert.Single(purge.Links!).To);
        Assert.DoesNotContain(Validator.Check(Atlas.Load(atlas.Root)), i => i.Error);

        // The instance catches up: the drift goes, and what only it knew and has dropped is retired.
        var agreed = ShopCatalog(200);
        LiveDb.Apply(atlas, Test01, agreed);
        Assert.Empty(db.Drift!);
        Assert.Empty(customers.Drift!);
        Assert.Equal("retired", purge.Status);
        Assert.Null(atlas.Entries["databases/shop/tables/dbo/orderaudit"].Status);
    }

    [Fact]
    public void A_database_found_only_on_an_instance_gets_entries_and_one_question_about_its_source()
    {
        var atlas = WithInstance(EmptyAtlas());
        LiveDb.Apply(atlas, Test01, ShopCatalog());
        LiveDb.Apply(atlas, Test01, ShopCatalog(200));
        Assert.Equal(11, atlas.Entries.Count);
        var db = atlas.Entries["databases/shop/database"];
        var q = Assert.Single(QuestionList.Parse(db.Body).Items);
        Assert.Equal(("cli", "person", "Where the source of database Shop lives. It was found only on instance sql-test-01; no scanned repository defines it."), (q.RaisedBy, q.WaitingOn, q.Missing));
        Assert.Null(db.Drift);
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
    }

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, null, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void A_production_instance_is_read_only_when_explicitly_allowed(bool production, bool? allowed, bool read)
    {
        var yaml = $"organisation: Test\ninstances:\n  - name: sql-01\n    server: sql-01.acme.local\n    production: {(production ? "true" : "false")}\n" + (allowed is { } a ? $"    allowed: {(a ? "true" : "false")}\n" : "");
        var instance = AtlasConfig.Parse(yaml).Instances.Single();
        Assert.Equal(read, LiveDb.Refusal(instance) is null);
    }

    [Fact]
    public void Only_listed_and_allowed_instances_are_ever_connected_to()
    {
        var atlas = WithInstance(EmptyAtlas(), "  - {name: sql-prod-01, server: sql-prod-01.acme.local, environment: production, production: true}\n");
        Assert.Contains("not listed", Assert.Throws<UsageException>(() => LiveDb.Read(atlas, "sql-seen-in-a-config-file")).Message);
        Assert.Contains("production", Assert.Throws<UsageException>(() => LiveDb.Read(atlas, "sql-prod-01")).Message);
    }

    [Fact]
    public void The_connection_is_named_short_lived_read_only_and_trusts_a_certificate_only_when_asked()
    {
        Assert.Equal("CARTOGRAPHER_SQL_SQL_TEST_01_ACME", LiveDb.EnvironmentVariable("sql-test-01.acme"));

        var integrated = new SqlConnectionStringBuilder(LiveDb.ConnectionString(Test01, null));
        Assert.Equal(("sql-test-01.acme.local", true, "Cartographer", ApplicationIntent.ReadOnly, 15, 30, false),
            (integrated.DataSource, integrated.IntegratedSecurity, integrated.ApplicationName, integrated.ApplicationIntent, integrated.ConnectTimeout, integrated.CommandTimeout, integrated.TrustServerCertificate));

        var supplied = new SqlConnectionStringBuilder(LiveDb.ConnectionString(Test01, "Server=localhost,14333;User Id=reader;TrustServerCertificate=true;Application Name=Other;Connect Timeout=600"));
        Assert.Equal(("localhost,14333", false, "Cartographer", ApplicationIntent.ReadOnly, 15, true),
            (supplied.DataSource, supplied.IntegratedSecurity, supplied.ApplicationName, supplied.ApplicationIntent, supplied.ConnectTimeout, supplied.TrustServerCertificate));
    }

    [Fact]
    public void An_unchanged_fingerprint_skips_the_rebuild_and_a_changed_one_does_not()
    {
        var atlas = WithInstance(EmptyAtlas());
        Assert.Equal("Shop: 11 entries read, permission full", LiveDb.Apply(atlas, Test01, ShopCatalog()));
        atlas.Save();
        var state = File.ReadAllText(Path.Combine(atlas.Root, "databases", "shop", "scan-state.yml"));
        Assert.Contains("database: databases/shop/database", state);
        Assert.Matches("sql-test-01:\n    fingerprint: [0-9a-f]{64}\n    read: \"2026-01-01\"\n    permission: full\n    entries:", state);

        // The second read brings only the object list (CatalogReader stops there); nothing is rebuilt from it.
        var second = new DbCatalog { Database = "Shop", Objects = ShopCatalog().Objects };
        Assert.Equal(ShopCatalog().Fingerprint, LiveDb.KnownFingerprint(atlas, Test01, "Shop"));
        Assert.Equal("Shop: unchanged since the read of 2026-01-01; not rebuilt", LiveDb.Apply(atlas, Test01, second));
        Assert.Empty(atlas.Save());
        Assert.Equal(11, atlas.Entries.Values.Count(e => e.Status is null));

        var changed = ShopCatalog();
        changed.Objects[0] = changed.Objects[0] with { Modified = Modified.AddDays(1) };
        Assert.NotEqual(ShopCatalog().Fingerprint, changed.Fingerprint);
        Assert.StartsWith("Shop: 11 entries read", LiveDb.Apply(atlas, Test01, changed));
    }

    [Fact]
    public void A_failed_read_is_recorded_with_its_reason_and_removes_nothing()
    {
        var atlas = WithInstance(EmptyAtlas());
        LiveDb.Apply(atlas, Test01, ShopCatalog());
        var db = atlas.Entries["databases/shop/database"];
        db.Instances!.Single().Read = "2025-12-25";
        // Nothing listens on port 9 of this machine.
        var (code, text) = LiveDb.Read(atlas, "sql-test-01", connectionString: "Server=127.0.0.1,9;User Id=nobody;Password=not-a-real-one;Connect Timeout=600");
        Assert.Equal(1, code);
        Assert.StartsWith("sql-test-01: read failed: ", text);
        Assert.DoesNotContain("not-a-real-one", text);

        Assert.Equal("2025-12-25", db.Instances!.Single().Read);
        Assert.Matches(@"- Live read of sql-test-01 failed on 2026-01-01: .+\. What is recorded is from the read of 2025-12-25\.", db.Body);
        Assert.DoesNotContain("Live read of sql-test-01:", db.Body);
        Assert.Equal(11, atlas.Entries.Values.Count(e => e.Status is null));
        var state = DbScanState.Load(atlas, db.Id).Instances["sql-test-01"];
        Assert.Equal(("2026-01-01", "2026-01-01", 11), (state.Read, state.Failed, state.Entries.Count));
        Assert.NotNull(state.Reason);
        Assert.DoesNotContain("not-a-real-one", string.Concat(Directory.EnumerateFiles(atlas.Root, "*", SearchOption.AllDirectories).Select(File.ReadAllText)));
    }

    [Fact]
    public void The_printed_queries_read_catalog_views_only_and_the_role_script_grants_only_view_definition()
    {
        var queries = CatalogQueries.Print();
        foreach (var view in new[] { "sys.databases", "sys.schemas", "sys.tables", "sys.objects", "sys.columns", "sys.indexes", "sys.foreign_keys", "sys.foreign_key_columns", "sys.triggers", "sys.sql_modules", "sys.parameters" })
            Assert.Contains(view, queries);
        // Every FROM and JOIN names a catalog view, and nothing counts rows or measures size.
        Assert.All(Regex.Matches(queries, @"\b(?:FROM|JOIN)\s+(\S+)").Select(m => m.Groups[1].Value), source => Assert.StartsWith("sys.", source));
        Assert.DoesNotMatch(@"(?i)\b(insert|update|delete|exec|count\(|sys\.partitions|dm_db_|sp_spaceused)\b", Regex.Replace(queries, "--.*", ""));

        var grants = CatalogQueries.RoleScript.Split('\n').Where(l => l.StartsWith("GRANT")).ToList();
        Assert.Equal(["GRANT VIEW DEFINITION TO cartographer_reader;"], grants);
    }

    /// <summary>
    /// Opt-in: runs against a real SQL Server when CARTOGRAPHER_TEST_SQL holds a connection string for a throwaway
    /// server (see docs/live-databases.md). Without it the test does nothing and passes.
    /// </summary>
    [Fact]
    public void A_real_server_reads_into_the_fixture_atlas_with_the_deliberate_drift()
    {
        var connectionString = Environment.GetEnvironmentVariable("CARTOGRAPHER_TEST_SQL");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Fixtures.Root(), "live-db"), "*.sql").Order(StringComparer.Ordinal))
                foreach (var batch in Regex.Split(File.ReadAllText(file), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(b => b.Trim().Length > 0))
                {
                    using var command = new SqlCommand(batch, connection);
                    command.ExecuteNonQuery();
                }
        }

        var atlas = WithInstance(Fixtures.Scan(Shop), "  - {name: sql-test-01, server: localhost, environment: test, databases: [Shop, Ledger]}\n");
        var before = atlas.Entries.Count;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var (code, text) = LiveDb.Read(atlas, "sql-test-01", connectionString: connectionString);
        Console.WriteLine($"live read took {timer.ElapsedMilliseconds} ms:\n{text}");
        Assert.True(code == 0, text);
        Assert.Matches(@"Ledger: 3 entries read, permission \w+\nShop: 12 entries read, permission \w+\n", text.Replace("\r", ""));

        // Everything the repositories define was found again under the same IDs; only the live-only table is new.
        Assert.Equal(before + 1, atlas.Entries.Count);
        Assert.Equal("instance:sql-test-01@" + Fixtures.Date, Assert.Single(atlas.Entries["databases/shop/tables/dbo/legacyimport"].Sources!).Value);
        Assert.Contains("| Payload | nvarchar(max) | yes |  |", atlas.Entries["databases/shop/tables/dbo/legacyimport"].Body);

        var drifted = atlas.Entries.Values.Where(e => e.Drift is { Count: > 0 }).ToDictionary(e => e.Id, e => e.Drift!.Single().What);
        Assert.Equal(["databases/shop/database", "databases/shop/tables/dbo/customers"], drifted.Keys);
        Assert.Equal("column Email is nvarchar(200) null in the repository and nvarchar(256) null on sql-test-01", drifted["databases/shop/tables/dbo/customers"]);
        Assert.Equal("found on sql-test-01 but not defined in the repository: table dbo.LegacyImport", drifted["databases/shop/database"]);

        var check = Validator.Check(Atlas.Load(atlas.Root));
        Assert.True(check.All(i => !i.Error), string.Join("\n", check));

        // Nothing changed on the server: the second read rebuilds nothing and writes nothing.
        (code, text) = LiveDb.Read(atlas, "sql-test-01", connectionString: connectionString);
        Assert.Equal((0, "Ledger: unchanged since the read of 2026-01-01; not rebuilt\nShop: unchanged since the read of 2026-01-01; not rebuilt\n"), (code, text.Replace("\r", "")));
    }
}
