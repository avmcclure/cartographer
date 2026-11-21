using System.Text;

namespace Cartographer.Tests;

public class LegacyTests
{
    const string Repo = "acme-legacy";

    /// <summary>Runs the whole mechanical pass over files given as (path, text) into a new atlas.</summary>
    static Atlas Scan(params (string Path, string Text)[] files) => Scan(files.Select(f => (f.Path, Encoding.UTF8.GetBytes(f.Text))).ToArray());

    static Atlas Scan((string Path, byte[] Bytes)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("repo-").FullName;
        foreach (var (path, bytes) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
            File.WriteAllBytes(Path.Combine(dir, path), bytes);
        }
        var atlasDir = Directory.CreateTempSubdirectory("atlas-").FullName;
        File.WriteAllText(Path.Combine(atlasDir, Atlas.ConfigFile), "organisation: Test\n");
        var atlas = Atlas.Load(atlasDir);
        Scanner.Run(atlas, Checkout.Open(Repo, dir));
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
        return atlas;
    }

    static Entry Component(Atlas atlas, string name) => atlas.Entries[Ids.Component(Repo, name)];
    static IEnumerable<string> Links(Entry e, string type) => (e.Links ?? []).Where(l => l.Type == type).Select(l => l.Name!);

    const string PublicClass = "VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nAttribute VB_Name = \"{0}\"\r\nAttribute VB_Creatable = True\r\nAttribute VB_Exposed = True\r\nOption Explicit\r\n";
    const string PrivateClass = "VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nAttribute VB_Name = \"{0}\"\r\nAttribute VB_Creatable = False\r\nAttribute VB_Exposed = False\r\nOption Explicit\r\n";

    [Fact]
    public void The_com_chain_fixture_gives_the_expected_atlas()
    {
        var atlas = Fixtures.Scan("com-chain");
        Fixtures.AssertMatchesExpected(atlas, "com-chain");

        var site = atlas.Entries["repositories/acme-intranet/components/acme-intranet"];
        var com = site.Links!.Single(l => l.How == "com");
        Assert.Equal(("repositories/acme-billing-com/components/acmebilling", "AcmeBilling.Invoice"), (com.To, com.Name));
        Assert.DoesNotContain("not-a-real-password", string.Join("\n", atlas.Entries.Values.Select(EntryFile.Write)));
    }

    [Fact]
    public void The_resolver_matches_a_com_call_to_a_provided_name_and_never_makes_it_a_package()
    {
        var dir = Directory.CreateTempSubdirectory("atlas-").FullName;
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile), "organisation: Test\n");
        var atlas = Atlas.Load(dir);
        var site = new Entry { Id = Ids.Component("web", "web"), Kind = Kinds.Component, Type = "site", Name = "web", Summary = "A site.", Repository = Ids.Repository("web") };
        site.Source("page", "web:default.asp@abc1234");
        site.Links =
        [
            new Link { Type = "calls", Name = "AcmeBilling.Invoice", How = "com", Basis = Basis.Extracted, Source = "page#L3" },
            new Link { Type = "calls", Name = "Vendor.Widget", How = "com", Basis = Basis.Extracted, Source = "page#L4" },
        ];
        atlas.Entries[site.Id] = site;
        Resolver.Run(atlas);
        Assert.Null(site.Packages);
        Assert.All(site.Links, l => Assert.Null(l.To));

        var library = new Entry { Id = Ids.Component("billing", "AcmeBilling"), Kind = Kinds.Component, Type = "library", Name = "AcmeBilling", Summary = "A library.", Repository = Ids.Repository("billing") };
        library.Source("proj", "billing:AcmeBilling.vbp@abc1234");
        library.Provides = [new Provided { Name = "AcmeBilling.Invoice", As = "progid", Basis = Basis.Extracted, Source = "proj" }];
        atlas.Entries[library.Id] = library;
        Resolver.Run(atlas);
        Assert.Equal(library.Id, site.Links.Single(l => l.Name == "AcmeBilling.Invoice").To);
        Assert.Null(site.Links.Single(l => l.Name == "Vendor.Widget").To);
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
    }

    [Fact]
    public void A_vb6_project_provides_its_public_classes_and_links_only_in_house_references()
    {
        // Windows-1252 bytes (the é in the description is not valid UTF-8) and Windows line endings, as VB6 saves them.
        var vbp = "Type=OleExe\r\nReference=*\\G{00020430-0000-0000-C000-000000000046}#2.0#0#C:\\WINDOWS\\system32\\stdole2.tlb#OLE Automation\r\n" +
                  "Reference=*\\G{11111111-2222-3333-4444-555555555555}#1.2#0#..\\lib\\AcmeTax.dll#Acme tax rules\r\n" +
                  "Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX\r\n" +
                  "Object={22222222-2222-3333-4444-555555555555}#1.0#0; AcmeGrid.ocx\r\n" +
                  "Class=Order; Order.cls\r\nClass=Helper; src\\Helper.cls\r\nForm=frmMain.frm\r\nStartup=\"Sub Main\"\r\nExeName32=\"AcmeOrders.exe\"\r\nName=\"AcmeOrders\"\r\nDescription=\"Café orders\"\r\n";
        var atlas = Scan(
        [
            ("orders/AcmeOrders.vbp", Encoding.Latin1.GetBytes(vbp)),
            ("orders/Order.cls", Encoding.Latin1.GetBytes(string.Format(PublicClass, "Order") + "' Commandé par le café\r\n")),
            ("orders/src/Helper.cls", Encoding.Latin1.GetBytes(string.Format(PrivateClass, "Helper"))),
            ("tools/Viewer.vbp", Encoding.Latin1.GetBytes("Type=Exe\r\nForm=frmView.frm\r\nName=\"AcmeViewer\"\r\n")),
        ]);

        var orders = Component(atlas, "AcmeOrders");
        Assert.Equal(("library", "vb6"), (orders.Type, orders.Stack!.Single()));
        Assert.Equal(["AcmeOrders.Order progid", "AcmeOrders.exe assembly"], orders.Provides!.Select(p => $"{p.Name} {p.As}"));
        Assert.All(orders.Provides!, p => Assert.Equal(Basis.Extracted, p.Basis));
        Assert.Equal("order-cls#L7", orders.Provides![0].Source);
        // Nothing provides them and no internal prefix is set, so the resolver lists both in-house files as packages.
        Assert.Equal(["AcmeGrid.ocx", "AcmeTax.dll"], orders.Packages);
        Assert.Contains("ActiveX EXE (`Type=OleExe`), startup object `Sub Main`", orders.Body);
        Assert.Contains("- 1 form, 0 modules, 2 classes.", orders.Body);
        Assert.Contains("- Other classes: `Helper` (private).", orders.Body);
        Assert.Contains("  - `AcmeTax.dll`: Acme tax rules; type library {11111111-2222-3333-4444-555555555555} version 1.2", orders.Body);
        Assert.Contains("- Platform references: `stdole2.tlb` (OLE Automation), `MSCOMCTL.OCX`.", orders.Body);

        var viewer = Component(atlas, "AcmeViewer");
        Assert.Equal("desktop-app", viewer.Type);
        Assert.Null(viewer.Provides);
    }

    [Fact]
    public void A_vb6_reference_resolves_to_the_project_that_builds_that_file()
    {
        var atlas = Scan(
            ("lib/AcmeTax.vbp", "Type=OleDll\nClass=Rates; Rates.cls\nExeName32=\"AcmeTax.dll\"\nName=\"AcmeTax\"\n"),
            ("lib/Rates.cls", string.Format(PublicClass, "Rates")),
            ("app/Till.vbp", "Type=Exe\nReference=*\\G{11111111-2222-3333-4444-555555555555}#1.0#0#..\\lib\\AcmeTax.dll#Acme tax rules\nName=\"Till\"\n"));
        var link = Component(atlas, "Till").Links!.Single();
        Assert.Equal(("depends-on", Ids.Component(Repo, "AcmeTax"), "AcmeTax.dll", "proj#L2"), (link.Type, link.To, link.Name, link.Source));
    }

    [Fact]
    public void An_asp_site_links_literal_progids_only()
    {
        var atlas = Scan(
            ("billing/Billing.vbp", "Type=OleDll\nName=\"Billing\"\n"),
            ("billing/notes.inc", "<% Set o = Server.CreateObject(\"Acme.InsideAnotherComponent\") %>\n"),
            ("www/default.asp",
                "<!-- #include file=\"inc/common.inc\" -->\n" +
                "<object runat=\"server\" id=\"cart\"\n   progid=\"Acme.Cart\"></object>\n" +
                "<object id=\"player\" progid=\"Acme.ClientSideOnly\"></object>\n" +
                "<%\n" +
                "Set a = Server.CreateObject(\"Acme.Orders\")\n" +
                "Set b = CreateObject(\"acme.orders\", \"appserver01\")\n" +
                "Set c = Server.CreateObject(\"ADODB.Recordset\")\n" +
                "Set d = CreateObject(\"Scripting.FileSystemObject\")\n" +
                "Set e = Server.CreateObject(\"Msxml2.ServerXMLHTTP.6.0\")\n" +
                "Set f = Server.CreateObject(progId)\n" +
                "Set g = Server.CreateObject(\"Acme.\" & kind)\n" +
                "' Set h = Server.CreateObject(\"Acme.CommentedOut\")\n" +
                "%>\n"),
            ("www/admin/users.asp", "<!-- #include virtual=\"/inc/common.inc\" -->\n<% Set m = Server.CreateObject(\"Acme.Membership\") %>\n"),
            ("www/inc/common.inc", "<% Set log = Server.CreateObject(\"Acme.Log\") %>\n"));

        var site = Component(atlas, "www");
        Assert.Equal(("site", "classic-asp"), (site.Type, site.Stack!.Single()));
        Assert.Equal(["Acme.Cart", "Acme.Log", "Acme.Membership", "Acme.Orders"], Links(site, "calls"));
        Assert.All(site.Links!, l => Assert.Equal(("com", Basis.Extracted, null), (l.How, l.Basis, l.To)));
        Assert.Equal("default-asp#L6", site.Links!.Single(l => l.Name == "Acme.Orders").Source);
        Assert.Equal("default-asp#L2", site.Links!.Single(l => l.Name == "Acme.Cart").Source);
        Assert.Contains("- Classic ASP: 2 pages, 1 other script file (`.asa`, `.inc`); `global.asa` not present.", site.Body);
        Assert.Contains("- Includes: 1 file is included by others. Most included, with the number of files including each: `www/inc/common.inc` (2).", site.Body);
        Assert.Contains("- Platform objects created: `ADODB.Recordset`, `Msxml2.ServerXMLHTTP.6.0`, `Scripting.FileSystemObject`.", site.Body);
        Assert.Contains("- 2 objects created from names built at run time, not linked: `www/default.asp` line 11, `www/default.asp` line 12.", site.Body);

        // An include file inside a VB6 project's directory belongs to that component, not to a site of its own.
        Assert.Equal(["Acme.InsideAnotherComponent"], Links(Component(atlas, "Billing"), "calls"));
        Assert.Equal(2, atlas.OfKind(Kinds.Component).Count());
    }

    [Fact]
    public void A_site_at_the_repository_root_takes_the_repository_name_and_lone_includes_make_no_site()
    {
        var atlas = Scan(("default.asp", "<% Response.Write \"hello\" %>\n"), ("global.asa", "<script runat=server></script>\n"), ("shared/x.inc", "<% %>\n"));
        var site = Component(atlas, Repo);
        Assert.Contains("- Classic ASP: 1 page, 2 other script files (`.asa`, `.inc`); `global.asa` present.", site.Body);
        Assert.Empty(Scan(("shared/x.inc", "<% %>\n")).OfKind(Kinds.Component));
    }

    [Fact]
    public void Stored_procedure_calls_with_literal_names_are_linked()
    {
        var atlas = Scan(
            ("web/default.asp",
                "<%\n" +
                "cmd.CommandText = \"dbo.usp_ListOrders\"\n" +
                "cmd.CommandType = 4\n" +
                "other.CommandText = \"NotAProcedureAsFarAsWeKnow\"\n" +
                "conn.Execute \"EXEC dbo.usp_Archive \" & orderId\n" +
                "conn.Execute \"exec usp_\" & suffix\n" +
                "conn.Execute \"EXECUTE [dbo].[usp_Purge]\"\n" +
                "' conn.Execute \"EXEC dbo.usp_CommentedOut\"\n" +
                "%>\n"),
            ("svc/Svc.vbp", "Type=OleDll\nName=\"Svc\"\n"),
            ("svc/Data.bas",
                "Sub Run()\n" +
                "    With cmd\n" +
                "        .CommandType = adCmdStoredProc\n" +
                "        .CommandText = \"usp_Reprice\"\n" +
                "    End With\n" +
                "    cmd2.CommandText = \"usp_\" & name\n" +
                "    cmd2.CommandType = adCmdStoredProc\n" +
                "End Sub\n"),
            ("svc/Net.cs",
                "class Net {\n" +
                "  void A() { var cmd = new SqlCommand(\"dbo.usp_Ship\", conn); cmd.CommandType = CommandType.StoredProcedure; }\n" +
                "  void B() { using var c2 = new SqlCommand(\"billing.usp_Refund\", conn) { CommandType = System.Data.CommandType.StoredProcedure }; }\n" +
                "  // var old = new SqlCommand(\"dbo.usp_Old\", conn) { CommandType = CommandType.StoredProcedure };\n" +
                "  void C() { cmd3.CommandText = \"dbo.usp_Count\"; cmd3.CommandType = CommandType.StoredProcedure; }\n" +
                "  void D() { var q = new SqlCommand(\"dbo.JustAName\", conn); }\n" +
                "}\n"),
            ("svc/Net.vb", "Dim cmd As New SqlCommand(\"dbo.usp_FromVb\", conn)\ncmd.CommandType = CommandType.StoredProcedure\n"),
            ("orphan/loose.bas", "conn.Execute \"EXEC dbo.usp_NoComponent\"\n"));

        var web = Component(atlas, "web");
        Assert.Equal(["dbo.usp_Archive", "dbo.usp_ListOrders", "dbo.usp_Purge"], Links(web, "calls"));
        Assert.All(web.Links!, l => Assert.Equal(("sql", Basis.Extracted), (l.How, l.Basis)));
        Assert.Equal("default-asp#L2", web.Links!.Single(l => l.Name == "dbo.usp_ListOrders").Source);
        Assert.Contains("- SQL built at run time, not linked: 1 statement in `web/default.asp` (1).", web.Body);

        var svc = Component(atlas, "Svc");
        Assert.Equal(["billing.usp_Refund", "dbo.usp_Count", "dbo.usp_FromVb", "dbo.usp_Ship", "usp_Reprice"], Links(svc, "calls"));
        Assert.DoesNotContain("usp_NoComponent", string.Join("\n", atlas.Entries.Values.Select(EntryFile.Write)));
    }

    [Fact]
    public void Literal_sql_gives_table_links_and_sql_built_at_run_time_is_only_counted()
    {
        var atlas = Scan(("web/report.asp",
            "<%\n" +
            "rs.Open \"SELECT o.Id, c.Name FROM dbo.Orders o INNER JOIN Customers AS c ON c.Id = o.CustomerId WHERE o.Total > 10\", conn\n" +
            "conn.Execute \"INSERT INTO Audit (What) VALUES ('report')\"\n" +
            "conn.Execute \"UPDATE o SET Printed = 1 FROM [dbo].[Orders] o WHERE o.Id = 1\"\n" +
            "conn.Execute \"DELETE FROM Staging\"\n" +
            "sql = \"SELECT * FROM Secret\" & suffix\n" +
            "sql = \"SELECT * \" & _\n      \"FROM Hidden\"\n" +
            "sql = \"DELETE FROM \" & tableName\n" +
            "label = \"Select an item\"\n" +
            "%>\n"));
        var web = Component(atlas, "web");
        Assert.Equal(["Customers", "dbo.Orders"], Links(web, "reads"));
        Assert.Equal(["Audit", "Staging", "dbo.Orders"], Links(web, "writes"));
        Assert.Equal("report-asp#L2", web.Links!.First(l => l.Type == "reads").Source);
        Assert.Contains("- SQL built at run time, not linked: 3 statements in `web/report.asp` (3).", web.Body);

        Assert.Equal([("reads", "a"), ("reads", "b.c"), ("reads", "d"), ("reads", "e"), ("writes", "t")],
            DbCallExtractor.Tables("insert into t (x)\n select 1 from a, b.c x left outer join d on d.i = ISNULL(x.i, a.i) where exists (select 1 from e)"));
    }

    [Fact]
    public void A_connection_string_gives_the_database_and_server_and_never_the_credential()
    {
        var atlas = Scan(
            ("web/db.asp",
                "<%\n" +
                "conn.Open \"Provider=SQLOLEDB;Data Source=SQL01\\PROD;Initial Catalog=Sales;User ID=web_user;Password=Sup3rS3cret!;\"\n" +
                "other.Open \"Driver={SQL Server};Server=\" & host & \";Database=Stock;Trusted_Connection=yes\"\n" +
                "third.Open \"Server=SQL02;Database=\" & dbName\n" +
                "%>\n"),
            ("web/Db.cs", "class Db { string s = @\"Server=tcp:sql03,1433;Database=Ledger;Uid=svc;Pwd=hunter2hunter2\"; string t = $\"Server=x;Database={name};\"; }\n"));
        var web = Component(atlas, "web");
        Assert.Equal(["Ledger", "Sales", "Stock"], Links(web, "uses"));
        Assert.Contains("- Connection string in `web/db.asp` line 2: server `SQL01\\PROD`, database `Sales`; a credential is in the code (not recorded here).", web.Body);
        Assert.Contains("- Connection string in `web/db.asp` line 3: server not a literal, database `Stock`; integrated sign-in, no credential.", web.Body);
        Assert.Contains("- Connection string in `web/Db.cs` line 1: server `tcp:sql03,1433`, database `Ledger`; a credential is in the code (not recorded here).", web.Body);

        var written = EntryFile.Write(web);
        foreach (var secret in new[] { "Sup3rS3cret", "hunter2", "web_user", "svc", "Password", "Pwd" }) Assert.DoesNotContain(secret, written);
    }

    [Fact]
    public void String_literals_are_read_the_way_each_language_writes_them()
    {
        var vb = DbCallExtractor.Literals("x = \"say \"\"hi\"\"\" & y ' \"a comment\"\nz = \"unterminated\nw = \"last\"\n", csharp: false);
        Assert.Equal([(1, "say \"hi\"", true), (3, "last", false)], vb.Select(l => (l.Line, l.Value, l.JoinedAfter)));

        var cs = DbCallExtractor.Literals("var a = \"q\\\"q\"; // \"no\"\n/* \"no\"\n */ var b = @\"two\nlines \"\"x\"\"\" +\n  $\"{id}\"; char c = '\"'; var d = \"end\";\n", csharp: true);
        Assert.Equal([(1, "q\"q", false, false), (3, "two\nlines \"x\"", true, false), (5, "{id}", false, true), (5, "end", false, false)], cs.Select(l => (l.Line, l.Value, l.JoinedAfter, l.Interpolated)));
        Assert.True(cs[2].JoinedBefore);
    }
}
