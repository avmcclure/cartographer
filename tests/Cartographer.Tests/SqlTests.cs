namespace Cartographer.Tests;

public class SqlTests
{
    static SqlSchema Schema(params string[] scripts)
    {
        var schema = new SqlSchema();
        for (var i = 0; i < scripts.Length; i++) schema.Apply(SqlParser.Parse(scripts[i]), $"{i + 1}.sql");
        return schema;
    }

    static SqlRoutine Routine(string sql) => Assert.Single(SqlParser.Parse(sql).Routines);
    static string[] Names(IEnumerable<SqlRef> refs) => refs.Select(r => (r.Name.Database is null ? "" : r.Name.Database + ".") + r.Name).ToArray();

    [Fact]
    public void One_schema_in_three_forms_gives_the_expected_atlas()
    {
        var atlas = Fixtures.Scan("one-schema-three-forms");
        Fixtures.AssertMatchesExpected(atlas, "one-schema-three-forms");
    }

    [Fact]
    public void A_table_gives_its_columns_keys_and_foreign_keys()
    {
        var script = SqlParser.Parse("""
            CREATE TABLE [Sales].[Order Lines] (
                Id int IDENTITY(1,1) NOT NULL,
                OrderId INT NOT NULL CONSTRAINT FK_Lines_Orders REFERENCES dbo.Orders (Id),
                "Sku" NVARCHAR (50) NULL,
                Price decimal(18, 2),
                Total AS (Price * 2),
                Notes [varchar](max),
                CONSTRAINT PK_Lines PRIMARY KEY CLUSTERED (Id),
                FOREIGN KEY (Sku) REFERENCES Products (Sku)
            );
            """);
        Assert.Empty(script.Errors);
        var t = Assert.Single(script.Tables);
        Assert.Equal(("Sales", "Order Lines", 1), (t.Name.Schema, t.Name.Name, t.Line));
        Assert.Equal(
            "| Column | Type | Nullable | Key |\n|---|---|---|---|\n| Id | int | no | PK |\n| OrderId | int | no | FK |\n| Sku | nvarchar(50) | yes | FK |\n" +
            "| Price | decimal(18,2) | yes |  |\n| Total | computed | yes |  |\n| Notes | varchar(max) | yes |  |\n", DbText.Columns(t.DbTextColumns()));
        Assert.Equal([("FK_Lines_Orders", "dbo.Orders", 3), (null, "dbo.Products", 9)], t.ForeignKeys.Select(f => (f.Name, f.References.ToString(), f.Line)));
    }

    [Fact]
    public void Batches_run_in_order_and_alter_and_drop_change_what_came_before()
    {
        var schema = Schema(
            "CREATE TABLE dbo.A (Id int PRIMARY KEY, Old int, Kept varchar(10) NOT NULL)\nGO\nCREATE TABLE dbo.B (Id int NOT NULL, AId int)\nGO\nCREATE TABLE dbo.Gone (Id int)\nGO\nCREATE VIEW dbo.V AS SELECT Id FROM dbo.A\nGO\nCREATE PROCEDURE dbo.P AS SELECT 1",
            """
            ALTER TABLE dbo.A ADD Added datetime2(7) NULL, Flag bit NOT NULL DEFAULT 0;
            ALTER TABLE dbo.A DROP COLUMN Old;
            ALTER TABLE dbo.A ALTER COLUMN Kept varchar(20) NULL;
            ALTER TABLE dbo.B ADD CONSTRAINT PK_B PRIMARY KEY (Id);
            ALTER TABLE dbo.B WITH CHECK ADD CONSTRAINT FK_B_A FOREIGN KEY (AId) REFERENCES dbo.A (Id), CONSTRAINT FK_B_A2 FOREIGN KEY (AId) REFERENCES dbo.A (Id);
            ALTER TABLE dbo.B DROP CONSTRAINT FK_B_A2;
            ALTER TABLE dbo.Missing ADD X int;
            DROP TABLE dbo.Gone;
            DROP VIEW IF EXISTS dbo.V;
            DROP PROCEDURE dbo.P;
            """);
        Assert.Equal(["dbo.a", "dbo.b"], schema.Tables.Keys);
        Assert.Empty(schema.Views);
        Assert.Empty(schema.Routines);
        Assert.Equal(["Id int no PK", "Kept varchar(20) yes ", "Added datetime2(7) yes ", "Flag bit no "], schema.Tables["dbo.a"].DbTextColumns().Select(c => $"{c.Name} {c.Type} {(c.Nullable ? "yes" : "no")} {c.Key}"));
        var b = schema.Tables["dbo.b"];
        Assert.Equal(["PK", "FK"], b.DbTextColumns().Select(c => c.Key));
        var fk = Assert.Single(b.ForeignKeys);
        Assert.Equal(("FK_B_A", "2.sql", 5, "1.sql"), (fk.Name, fk.File, fk.Line, b.File));
    }

    [Fact]
    public void Create_or_alter_and_alter_replace_a_routine_and_schema_defaults_to_dbo()
    {
        var schema = Schema("CREATE PROC P @a int AS SELECT 1", "CREATE OR ALTER PROCEDURE [dbo].[P] @a int, @b nvarchar(20) = NULL OUTPUT AS SELECT 2\nGO\nALTER FUNCTION dbo.F (@x int) RETURNS int AS BEGIN RETURN @x END");
        var p = schema.Routines["dbo.p"];
        Assert.Equal(("2.sql", false, "dbo.P"), (p.File, p.IsFunction, p.Name.ToString()));
        Assert.Equal("| Parameter | Type | Direction |\n|---|---|---|\n| @a | int | in |\n| @b | nvarchar(20) | out |\n", DbText.Parameters(p.Parameters));
        Assert.True(schema.Routines["dbo.f"].IsFunction);
    }

    [Fact]
    public void A_body_gives_reads_writes_and_calls_but_not_temp_tables_table_variables_or_ctes()
    {
        var r = Routine("""
            CREATE PROCEDURE dbo.Work @id int AS
            BEGIN
                DECLARE @t TABLE (Id int);
                CREATE TABLE #scratch (Id int);
                INSERT INTO #scratch SELECT Id FROM dbo.Orders;
                INSERT @t SELECT Id FROM #scratch;
                WITH recent AS (SELECT Id FROM [Sales].[Invoices] WHERE Total > dbo.MinTotal(@id))
                INSERT INTO dbo.Archive (Id) SELECT r.Id FROM recent r JOIN @t t ON t.Id = r.Id;
                UPDATE o SET o.Done = 1 FROM dbo.Orders o JOIN Customers c ON c.Id = o.CustomerId;
                DELETE FROM dbo.Queue WHERE Id = @id;
                UPDATE v SET v.Id = 0 FROM @t v JOIN dbo.Orders o ON o.Id = v.Id;
                DELETE q FROM (SELECT Id FROM dbo.Queue) q;
                MERGE dbo.Stock AS target USING Warehouse.dbo.Deliveries AS source ON target.Id = source.Id
                    WHEN MATCHED THEN UPDATE SET Qty = source.Qty WHEN NOT MATCHED THEN INSERT (Id) VALUES (source.Id);
                SELECT x.n.value('.', 'int') FROM @x.nodes('/a') x(n) CROSS APPLY dbo.Split(@id) s;
                SELECT * INTO dbo.Snapshot FROM dbo.Orders;
                TRUNCATE TABLE dbo.Queue;
                EXEC dbo.Notify @id;
                EXECUTE Ledger.dbo.Post @id;
                EXEC Linked.Db.dbo.Remote;
            END
            """);
        Assert.Equal(["dbo.Orders", "Sales.Invoices", "dbo.Customers", "dbo.Queue", "Warehouse.dbo.Deliveries"], Names(r.Reads));
        Assert.Equal(["dbo.Archive", "dbo.Orders", "dbo.Queue", "dbo.Stock", "dbo.Snapshot"], Names(r.Writes));
        Assert.Equal(["dbo.MinTotal", "dbo.Split", "dbo.Notify", "Ledger.dbo.Post"], Names(r.Calls));
        Assert.Equal(5, r.Reads[0].Line);
        Assert.Equal(0, r.DynamicSql);
    }

    [Fact]
    public void Dynamic_sql_is_counted_and_not_followed()
    {
        var script = SqlParser.Parse("""
            EXEC('CREATE TABLE dbo.Hidden (Id int)');
            GO
            CREATE PROCEDURE dbo.Dyn @sql nvarchar(max), @proc sysname AS
                EXEC (@sql);
                EXEC sp_executesql N'SELECT * FROM dbo.Secret';
                EXEC @proc;
                EXEC dbo.Real;
            """);
        var r = Assert.Single(script.Routines);
        Assert.Equal((1, 3, "dbo.Real"), (script.DynamicSql, r.DynamicSql, Assert.Single(r.Calls).Name.ToString()));
        Assert.Empty(r.Reads);
        Assert.Empty(script.Tables);
    }

    [Fact]
    public void A_trigger_gives_its_table_events_and_body()
    {
        var script = SqlParser.Parse("""
            CREATE TRIGGER trg_Orders_Audit ON Sales.Orders AFTER INSERT, UPDATE AS
                INSERT INTO dbo.Audit (OrderId) SELECT i.Id FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id;
            GO
            CREATE TRIGGER dbo.trg_View ON dbo.V INSTEAD OF DELETE AS EXEC dbo.Refuse;
            GO
            CREATE TRIGGER ddl_guard ON DATABASE FOR DROP_TABLE AS ROLLBACK;
            """);
        Assert.Empty(script.Errors);
        Assert.Equal(2, script.Triggers.Count());
        var t = script.Triggers.First();
        Assert.Equal(("Sales.trg_Orders_Audit", "Sales.Orders", "after", "insert,update"), (t.Name.ToString(), t.Table.ToString(), t.Timing, string.Join(',', t.Events)));
        Assert.Equal(["dbo.Audit"], Names(t.Writes));
        Assert.Empty(t.Reads);
        var v = script.Triggers.Last();
        Assert.Equal(("instead of", "delete", "dbo.Refuse"), (v.Timing, v.Events.Single(), v.Calls.Single().Name.ToString()));
    }

    [Fact]
    public void A_view_reads_its_tables_and_definitions_inside_if_blocks_are_found()
    {
        var script = SqlParser.Parse("""
            IF OBJECT_ID('dbo.T') IS NULL
            BEGIN
                CREATE TABLE dbo.T (Id int);
            END
            GO
            CREATE VIEW dbo.V AS SELECT t.Id FROM dbo.T t JOIN Other..Things x ON x.Id = t.Id;
            """);
        Assert.Single(script.Tables);
        Assert.Equal(["dbo.T", "Other.dbo.Things"], Names(Assert.Single(script.Views).Reads));
    }

    [Theory]
    [InlineData("USE master;\nGO\nCREATE DATABASE Shop;\nGO\nUSE [Shop];\nGO\nCREATE TABLE dbo.T (Id int);\nGO\nUSE Other;", "Shop")]
    [InlineData("CREATE DATABASE Shop;\nGO\nCREATE TABLE dbo.T (Id int);", "Shop")]
    [InlineData("USE Shop;\nINSERT INTO dbo.T VALUES (1);", "Shop")]
    [InlineData("CREATE TABLE dbo.T (Id int);", null)]
    public void The_database_a_script_is_for(string sql, string? database) => Assert.Equal(database, SqlParser.Parse(sql).Database);

    [Fact]
    public void Bad_sql_is_reported_not_thrown_and_the_good_batches_still_count()
    {
        var script = SqlParser.Parse("CREATE TABLE dbo.Good (Id int);\nGO\nCREATE TABLE dbo.Bad (Id int,,;\nGO\nCREATE TABLE dbo.AlsoGood (Id int);");
        Assert.Equal(3, Assert.Single(script.Errors).Line);
        Assert.Equal(["Good", "AlsoGood"], script.Tables.Select(t => t.Name.Name));
        Assert.NotEmpty(SqlParser.Parse(":r .\\other.sql\nthis is not sql").Errors);
        Assert.Empty(SqlParser.Parse("").Changes);
    }
}
