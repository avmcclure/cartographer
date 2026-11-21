namespace Cartographer;

/// <summary>
/// Every statement Cartographer ever sends to a SQL Server instance. All of them read catalog views only: no table
/// data, no row counts, no sizes. `cartographer db queries` prints this list for a DBA to review.
/// </summary>
public static class CatalogQueries
{
    /// <summary>Run once per instance, and only when cartographer.yml lists no databases for it.</summary>
    public const string Databases = """
        SELECT name
        FROM sys.databases
        WHERE name NOT IN ('master', 'model', 'msdb', 'tempdb')
          AND state_desc = 'ONLINE'
          AND HAS_DBACCESS(name) = 1
        ORDER BY name;
        """;

    public const string Permissions = """
        SELECT DB_NAME() AS database_name,
               HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION') AS view_definition,
               HAS_PERMS_BY_NAME('sys.sql_expression_dependencies', 'OBJECT', 'SELECT') AS dependencies;
        """;

    /// <summary>U table, V view, P procedure, FN/IF/TF function, TR trigger on a table or view. CLR objects are not read.</summary>
    public const string Objects = """
        SELECT s.name AS schema_name, o.name, RTRIM(o.type) AS type, o.modify_date
        FROM sys.objects AS o
        JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF', 'TR')
          AND o.is_ms_shipped = 0
        ORDER BY s.name, o.name;
        """;

    public const string Columns = """
        SELECT s.name AS schema_name, t.name AS table_name, c.name,
               CASE WHEN ty.is_user_defined = 1 THEN SCHEMA_NAME(ty.schema_id) + '.' + ty.name ELSE ty.name END AS type,
               c.max_length, c.precision, c.scale, c.is_nullable, c.is_computed,
               CASE WHEN EXISTS (
                   SELECT 1
                   FROM sys.indexes AS i
                   JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                   WHERE i.object_id = c.object_id AND i.is_primary_key = 1 AND ic.column_id = c.column_id
               ) THEN 1 ELSE 0 END AS is_primary_key
        FROM sys.columns AS c
        JOIN sys.tables AS t ON t.object_id = c.object_id
        JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name, c.column_id;
        """;

    public const string ForeignKeys = """
        SELECT s.name AS schema_name, t.name AS table_name, fk.name, fk.is_system_named, c.name AS column_name,
               rs.name AS referenced_schema, rt.name AS referenced_table
        FROM sys.foreign_keys AS fk
        JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
        JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        JOIN sys.columns AS c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
        JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
        ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id;
        """;

    /// <summary>One row per trigger and event (INSERT, UPDATE, DELETE).</summary>
    public const string Triggers = """
        SELECT s.name AS schema_name, tr.name, p.name AS parent_name, tr.is_instead_of_trigger, te.type_desc AS event
        FROM sys.triggers AS tr
        JOIN sys.objects AS p ON p.object_id = tr.parent_id
        JOIN sys.schemas AS s ON s.schema_id = p.schema_id
        JOIN sys.trigger_events AS te ON te.object_id = tr.object_id
        WHERE tr.parent_class = 1
          AND tr.is_ms_shipped = 0
        ORDER BY s.name, tr.name, te.type;
        """;

    /// <summary>The text of views, procedures, functions and triggers. NULL when the identity may not see it, or when it is encrypted.</summary>
    public const string Modules = """
        SELECT s.name AS schema_name, o.name, m.definition, OBJECTPROPERTY(o.object_id, 'IsEncrypted') AS is_encrypted
        FROM sys.objects AS o
        JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        LEFT JOIN sys.sql_modules AS m ON m.object_id = o.object_id
        WHERE o.type IN ('V', 'P', 'FN', 'IF', 'TF', 'TR')
          AND o.is_ms_shipped = 0
        ORDER BY s.name, o.name;
        """;

    public const string Parameters = """
        SELECT s.name AS schema_name, o.name AS routine_name, p.name,
               CASE WHEN ty.is_user_defined = 1 THEN SCHEMA_NAME(ty.schema_id) + '.' + ty.name ELSE ty.name END AS type,
               p.max_length, p.precision, p.scale, p.is_output
        FROM sys.parameters AS p
        JOIN sys.objects AS o ON o.object_id = p.object_id
        JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        JOIN sys.types AS ty ON ty.user_type_id = p.user_type_id
        WHERE o.type IN ('P', 'FN', 'IF', 'TF')
          AND o.is_ms_shipped = 0
          AND p.parameter_id > 0
        ORDER BY s.name, o.name, p.parameter_id;
        """;

    static readonly (string Title, string Sql)[] All =
    [
        ("Databases the identity can open (only when cartographer.yml lists none for the instance)", Databases),
        ("What the identity may see in this database", Permissions),
        ("Tables, views, procedures, functions and triggers, with their last-modified dates (the schema fingerprint)", Objects),
        ("Table columns, their types and primary keys", Columns),
        ("Foreign keys", ForeignKeys),
        ("Triggers on tables and views, and the events they fire on", Triggers),
        ("Definitions of views, procedures, functions and triggers", Modules),
        ("Procedure and function parameters", Parameters),
    ];

    /// <summary>What `db queries` prints.</summary>
    public static string Print() =>
        "-- Cartographer: every query a live read (`cartographer db read`) runs. Nothing else is ever sent.\n" +
        "-- They read catalog views only: never table data, row counts or sizes.\n" +
        "-- The connection sets Application Name=Cartographer and ApplicationIntent=ReadOnly, with a 15 second connect and a 30 second command timeout.\n" +
        "-- The first query runs once per instance, the rest once in each database read. When the third shows nothing has\n" +
        "-- changed since the last read, the ones after it are not run.\n" +
        string.Concat(All.Select((q, i) => $"\n-- {i + 1}. {q.Title}\n{q.Sql}\n"));

    /// <summary>What `db role-script` prints.</summary>
    public const string RoleScript = """
        -- Cartographer: a least-privilege identity for reading schema metadata.
        -- Review it, replace the names in <angle brackets>, and run it as a server administrator.
        -- It grants no access to table data: VIEW DEFINITION shows what objects exist and how they are defined, nothing else.
        -- The queries it serves are printed by: cartographer db queries

        -- 1. Once per server: the login Cartographer connects as.
        --    A Windows account or group is preferred, because there is no password to look after.
        USE master;
        CREATE LOGIN [<DOMAIN\account>] FROM WINDOWS;
        --    Or a SQL login. Choose the password yourself and keep it out of source control:
        -- CREATE LOGIN [<login>] WITH PASSWORD = N'<a password you choose>';

        -- 2. In each database Cartographer may read:
        USE [<database>];
        CREATE ROLE cartographer_reader;
        GRANT VIEW DEFINITION TO cartographer_reader;
        CREATE USER [<DOMAIN\account>] FOR LOGIN [<DOMAIN\account>];
        ALTER ROLE cartographer_reader ADD MEMBER [<DOMAIN\account>];

        -- That is all a read needs. Its permission level is then recorded as "definitions".

        -- OPTIONAL, per database: lets the read record the level "full".
        -- Cartographer only checks whether it holds this permission; v1 takes dependencies from the definitions themselves.
        -- GRANT SELECT ON sys.sql_expression_dependencies TO cartographer_reader;

        -- OPTIONAL, server level, instead of step 2 in every database (SQL Server 2014 and later):
        -- the login may open every database and see every definition, including in databases created later.
        -- USE master;
        -- GRANT CONNECT ANY DATABASE TO [<DOMAIN\account>];
        -- GRANT VIEW ANY DEFINITION TO [<DOMAIN\account>];

        -- SQL Agent jobs and linked servers are not read in v1, so no grant for them is included.

        """;
}
