using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <param name="Key">"", "PK", "FK" or "PK, FK".</param>
public sealed record Column(string Name, string Type, bool Nullable, string Key = "");
public sealed record Parameter(string Name, string Type, bool Output);

/// <summary>
/// The shared wording of extracted blocks on database entries, so that a schema read from a repository and one read
/// from a live instance produce the same text and can be compared.
/// Names: a table, view, routine or trigger entry's `name` is "schema.Name" in the source's capitalisation; its ID is
/// Ids.DbObject(database, "tables" | "routines" | "triggers", schema, name). Types are lower-case with length, e.g. "nvarchar(50)", "decimal(18,2)", "varchar(max)".
/// </summary>
public static class DbText
{
    public static string Columns(IEnumerable<Column> columns)
    {
        var sb = new StringBuilder("| Column | Type | Nullable | Key |\n|---|---|---|---|\n");
        foreach (var c in columns) sb.Append($"| {c.Name} | {c.Type} | {(c.Nullable ? "yes" : "no")} | {c.Key} |\n");
        return sb.ToString();
    }

    /// <summary>Reads back the columns table from an entry body; empty when there is none.</summary>
    public static List<Column> ParseColumns(string body)
    {
        var columns = new List<Column>();
        var inTable = false;
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("| Column | Type | Nullable | Key |")) { inTable = true; continue; }
            if (!inTable) continue;
            if (!line.StartsWith('|')) break;
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length == 4 && !Regex.IsMatch(cells[0], "^-+$")) columns.Add(new(cells[0], cells[1], cells[2] == "yes", cells[3]));
        }
        return columns;
    }

    public static string Parameters(IEnumerable<Parameter> parameters)
    {
        var list = parameters.ToList();
        if (list.Count == 0) return "No parameters.\n";
        var sb = new StringBuilder("| Parameter | Type | Direction |\n|---|---|---|\n");
        foreach (var p in list) sb.Append($"| {p.Name} | {p.Type} | {(p.Output ? "out" : "in")} |\n");
        return sb.ToString();
    }

    /// <summary>A type name the same way from ScriptDom and from the catalog: lower-case, no spaces, no brackets.</summary>
    /// <summary>Defaults are written out, so "DECIMAL" in a script and "decimal(18,0)" from the catalog are the same type.</summary>
    public static string TypeName(string raw)
    {
        var t = Regex.Replace(raw.Replace("[", "").Replace("]", ""), @"\s+", "").ToLowerInvariant();
        return t switch
        {
            "rowversion" => "timestamp",
            "decimal" or "numeric" => t + "(18,0)",
            "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary" => t + "(1)",
            "datetime2" or "time" or "datetimeoffset" => t + "(7)",
            _ => Regex.Replace(t, @"^(decimal|numeric)\((\d+)\)$", "$1($2,0)"),
        };
    }
}
