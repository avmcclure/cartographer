using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Cartographer;

// The rows of the catalog queries, as plain data. Type is already in DbText.TypeName form (LiveDb.TypeName).
/// <param name="Type">U table, V view, P procedure, FN/IF/TF function, TR trigger.</param>
public sealed record DbObjectRow(string Schema, string Name, string Type, DateTime Modified);
public sealed record DbColumnRow(string Schema, string Table, string Name, string Type, bool Nullable, bool Computed = false, bool PrimaryKey = false);
/// <summary>One row per column of the key.</summary>
public sealed record DbForeignKeyRow(string Schema, string Table, string Name, bool SystemNamed, string Column, string ReferencedSchema, string ReferencedTable);
/// <summary>One row per event: "insert", "update" or "delete".</summary>
public sealed record DbTriggerRow(string Schema, string Name, string Table, bool InsteadOf, string Event);
/// <param name="Definition">Null when the identity may not see it, or when it is encrypted.</param>
public sealed record DbModuleRow(string Schema, string Name, string? Definition, bool Encrypted = false);
public sealed record DbParameterRow(string Schema, string Routine, string Name, string Type, bool Output = false);

/// <summary>What the catalog queries returned for one database. No connection is needed to turn this into entries.</summary>
public sealed class DbCatalog
{
    public string Database = "";
    /// <summary>The identity holds VIEW DEFINITION on the whole database; without it, objects can be missing from every list.</summary>
    public bool ViewDefinition = true;
    /// <summary>The identity may read sys.sql_expression_dependencies.</summary>
    public bool Dependencies = true;
    public List<DbObjectRow> Objects = [];
    public List<DbColumnRow> Columns = [];
    public List<DbForeignKeyRow> ForeignKeys = [];
    public List<DbTriggerRow> Triggers = [];
    public List<DbModuleRow> Modules = [];
    public List<DbParameterRow> Parameters = [];

    /// <summary>A hash over what the identity may see and every object's name and last-modified date: unchanged means nothing to rebuild.</summary>
    public string Fingerprint => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{ViewDefinition}|{Dependencies}\n" + string.Concat(Objects.Select(o => $"{o.Type}|{o.Schema}.{o.Name}|{o.Modified:O}\n").Order(StringComparer.Ordinal)))));
}

/// <summary>Runs the catalog queries. The only code that talks to a server.</summary>
public static class CatalogReader
{
    static List<T> Query<T>(SqlConnection connection, string sql, Func<SqlDataReader, T> row)
    {
        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(row(reader));
        return rows;
    }

    static bool Yes(object value) => value is true or 1;
    static string Type(SqlDataReader r, int at) => LiveDb.TypeName(r.GetString(at), Convert.ToInt32(r[at + 1]), Convert.ToInt32(r[at + 2]), Convert.ToInt32(r[at + 3]));

    public static List<string> Databases(SqlConnection connection) => Query(connection, CatalogQueries.Databases, r => r.GetString(0));

    /// <summary>Reads one database. When its fingerprint equals <paramref name="knownFingerprint"/>, only the object list is read.</summary>
    public static DbCatalog Read(SqlConnection connection, string database, Func<string, string?> knownFingerprint)
    {
        connection.ChangeDatabase(database);
        var catalog = Query(connection, CatalogQueries.Permissions, r => new DbCatalog { Database = r.GetString(0), ViewDefinition = Yes(r[1]), Dependencies = Yes(r[2]) })[0];
        catalog.Objects = Query(connection, CatalogQueries.Objects, r => new DbObjectRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDateTime(3)));
        if (catalog.Fingerprint == knownFingerprint(catalog.Database)) return catalog;
        catalog.Columns = Query(connection, CatalogQueries.Columns, r => new DbColumnRow(r.GetString(0), r.GetString(1), r.GetString(2), Type(r, 3), Yes(r[7]), Yes(r[8]), Yes(r[9])));
        catalog.ForeignKeys = Query(connection, CatalogQueries.ForeignKeys, r => new DbForeignKeyRow(r.GetString(0), r.GetString(1), r.GetString(2), Yes(r[3]), r.GetString(4), r.GetString(5), r.GetString(6)));
        catalog.Triggers = Query(connection, CatalogQueries.Triggers, r => new DbTriggerRow(r.GetString(0), r.GetString(1), r.GetString(2), Yes(r[3]), r.GetString(4).ToLowerInvariant()));
        catalog.Modules = Query(connection, CatalogQueries.Modules, r => new DbModuleRow(r.GetString(0), r.GetString(1), r[2] as string, Yes(r[3])));
        catalog.Parameters = Query(connection, CatalogQueries.Parameters, r => new DbParameterRow(r.GetString(0), r.GetString(1), r.GetString(2), Type(r, 3), Yes(r[7])));
        return catalog;
    }
}
