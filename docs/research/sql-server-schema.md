# Reading SQL Server schema from repositories and live databases

Research for issue #4. Every claim cites the primary source that owns it; unverified points are marked.

## Answer

- **Live database:** query the catalog views directly (`sys.foreign_keys` + `sys.foreign_key_columns`, `sys.triggers`, `sys.sql_modules`, `sys.sql_expression_dependencies`). Avoid `INFORMATION_SCHEMA`: it truncates routine bodies at 4000 characters and has no trigger view we found.
- **Permissions:** database-level `VIEW DEFINITION` covers FKs, triggers and module text. Dependencies additionally need `SELECT` on `sys.sql_expression_dependencies` (db_owner-only by default). Missing permission yields missing rows or `NULL` definitions, not errors.
- **Source control:** `.sqlproj` builds to a `.dacpac`, which DacFx reads as a queryable model. Loose, DbUp and Flyway scripts are imperative T-SQL: parse with ScriptDom (MIT), or replay into a scratch database and extract. EF Core holds FKs in the model snapshot, but procedures and triggers are raw SQL strings inside migrations.
- **Legacy .NET Framework:** EDMX, `.dbml` and typed-DataSet `.xsd` are XML that names tables, relationships and mapped procedures; config files give connection strings; ADO.NET code names procedures in `CommandText`.
- **Drift:** `sqlpackage /Action:DeployReport` (dacpac vs database, or dacpac vs dacpac) or DacFx `SchemaComparison`. `DriftReport` only works on DAC-registered databases.

## Live database

| Need | Source | Notes |
| --- | --- | --- |
| Foreign keys | [`sys.foreign_keys`](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-foreign-keys-transact-sql), [`sys.foreign_key_columns`](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-foreign-key-columns-transact-sql) | One row per constraint, with `is_disabled`, `is_not_trusted` and delete/update actions; the columns view gives the column pairs. |
| Triggers | [`sys.triggers`](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-triggers-transact-sql) | DML and database DDL triggers; `parent_id` is the table; `is_disabled`, `is_instead_of_trigger`. DDL triggers are visible only here. |
| Procedure / trigger text | [`sys.sql_modules`](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-sql-modules-transact-sql) | `definition` is `nvarchar(max)`; `NULL` when encrypted. Text is not updated by `sp_rename`. |
| What a module references | [`sys.sql_expression_dependencies`](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-sql-expression-dependencies-transact-sql) | By-name dependencies, including cross-database and cross-server names (IDs unresolved). T-SQL procedures and DML triggers are tracked as referencing entities; CLR triggers are not tracked. `referenced_id` can be `NULL` (missing object, caller-dependent). |

`INFORMATION_SCHEMA.ROUTINES.ROUTINE_DEFINITION` returns only the first 4000 characters, and Microsoft warns that these views expose only a subset of metadata ([ROUTINES](https://learn.microsoft.com/en-us/sql/relational-databases/system-information-schema-views/routines-transact-sql)).

**Permissions.** Catalog visibility is limited to securables the user owns or has some permission on; without it, queries return fewer rows and definition columns return `NULL`. `VIEW DEFINITION` at object, database or server scope grants visibility ([Metadata Visibility Configuration](https://learn.microsoft.com/en-us/sql/relational-databases/security/metadata-visibility-configuration)). `sys.sql_expression_dependencies` also requires `SELECT` on the view, granted by default only to `db_owner` (same page as above). `sqlpackage` Extract needs more: at least `ALTER ANY LOGIN`, database `VIEW DEFINITION`, and that `SELECT` ([Extract a DACPAC](https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/concepts/data-tier-applications/extract-dacpac-from-database)).

## Source control

- **SQL projects (`.sqlproj`).** Declarative `.sql` files, one per object; the build validates references between objects and outputs a `.dacpac`. SDK-style projects (`Microsoft.Build.Sql`) build with `dotnet build`; original SSDT projects use .NET Framework MSBuild ([SQL database projects](https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/sql-database-projects)).
- **Dacpac / DacFx.** `TSqlModel` loads from a dacpac, from a live database (`LoadFromDatabase`), or from T-SQL DDL added with `AddObjects`, and is queried with `GetObjects` ([TSqlModel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.dac.model.tsqlmodel)). One reader can therefore serve projects, loose declarative scripts and databases.
- **ScriptDom.** `TSqlParser.Parse` returns a `TSqlFragment` tree walked with `TSqlFragmentVisitor` ([API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.transactsql.scriptdom.tsqlparser)); MIT-licensed ([repo](https://github.com/microsoft/SqlScriptDOM)). It is a parser, so it should need no connection; the docs do not state this explicitly.
- **EF Core.** Each migration has `Up`/`Down`, a designer metadata file, and a shared `ModelSnapshot.cs` holding the current model. Procedures, functions, triggers and views are "database objects that EF Core isn't aware of", added via `migrationBuilder.Sql(...)`; `dotnet ef migrations script` emits SQL ([Managing Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)).
- **DbUp.** Applied scripts are journaled in a `SchemaVersions` table by default ([Journaling](https://dbup.readthedocs.io/en/latest/more-info/journaling/)).
- **Flyway.** `V<version>__<desc>.sql` versioned migrations recorded in `flyway_schema_history` ([versioned](https://documentation.red-gate.com/fd/versioned-migrations-273973333.html)); `R__<desc>.sql` repeatable migrations re-run on checksum change and suit views, procedures and functions ([repeatable](https://documentation.red-gate.com/fd/repeatable-migrations-273973335.html)).

Migration scripts are ordered deltas, so the final schema is only known by replaying them. The journal tables show which scripts a live database has received.

## Legacy .NET Framework sources

| Artifact | Reveals | Source |
| --- | --- | --- |
| EF6 `.edmx` | SSDL section lists tables, `AssociationSet` (stands for a foreign key constraint) and `Function` (a stored procedure in the database); MSL maps procedures to entities. | [SSDL spec](https://learn.microsoft.com/en-us/ef/ef6/modeling/designer/advanced/edmx/ssdl-spec), [MSL spec](https://learn.microsoft.com/en-us/ef/ef6/modeling/designer/advanced/edmx/msl-spec) |
| LINQ to SQL `.dbml` | Tables mapped to classes, associations (foreign-key relationships), and procedures/functions exposed as `DataContext` methods. | [Object model](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/linq/the-linq-to-sql-object-model), [O/R Designer](https://learn.microsoft.com/en-us/visualstudio/data-tools/linq-to-sql-tools-in-visual-studio2) |
| Typed DataSet `.xsd` | Tables, designer-defined relations, and TableAdapter commands that are either SQL text or existing stored procedures. | [Datasets](https://learn.microsoft.com/en-us/visualstudio/data-tools/create-and-configure-datasets-in-visual-studio), [TableAdapters](https://learn.microsoft.com/en-us/visualstudio/data-tools/create-and-configure-tableadapters) |
| `web.config` / `app.config` | `connectionStrings` entries (name, provider, server, database): which application talks to which database. May contain credentials, so record only server and database. | [Connection strings and configuration files](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/connection-strings-and-configuration-files) |
| ADO.NET code | With `CommandType.StoredProcedure`, `CommandText` is the procedure name; with the default `Text` it is inline SQL (parse with ScriptDom). | [SqlCommand.CommandType](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlcommand.commandtype) |

These describe what the application believed at design time, not the database: associations in EDMX, dbml and DataSets can exist without a real constraint. Unverified in primary docs.

Other coupling the server can reveal:

- **Linked servers:** `sys.servers` has one row per linked or remote server ([sys.servers](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-servers-transact-sql)).
- **SQL Agent jobs:** `msdb.dbo.sysjobsteps` stores each step's `subsystem`, `database_name` and `command` text ([sysjobsteps](https://learn.microsoft.com/en-us/sql/relational-databases/system-tables/dbo-sysjobsteps-transact-sql)).
- **SSIS:** packages are XML `.dtsx` files (or stored in msdb) containing connection managers ([Save packages](https://learn.microsoft.com/en-us/sql/integration-services/save-packages), [MS-DTSX](https://learn.microsoft.com/en-us/openspecs/sql_data_portability/ms-dtsx/a7d84cd1-4aca-433a-b450-58b331fca519)).

## Drift

- **`sqlpackage /Action:DeployReport`** writes an XML report of the changes a publish would make. Source is a dacpac; target is a database or another dacpac (`/TargetFile`) ([DeployReport](https://learn.microsoft.com/en-us/sql/tools/sqlpackage/sqlpackage-deploy-drift-report)).
- **`DriftReport`** reports changes "since it was last registered", so it needs a registered data-tier application (same page).
- **DacFx `SchemaComparison`** compares databases, dacpacs and SQL projects in code ([SchemaComparison](https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.dac.compare.schemacomparison)).
- For repos without a dacpac, compare normalised facts instead: the same FK, trigger and module rows extracted from both sides.

## Not verified

- Whether `INFORMATION_SCHEMA` exposes triggers at all (no trigger view found; not confirmed against the full view list).
- Whether dynamic SQL inside procedures is invisible to `sys.sql_expression_dependencies` (implied by "by-name dependency", not stated).
- Read permissions for `msdb` job tables and `sys.servers`.
