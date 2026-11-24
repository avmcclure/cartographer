using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>
/// Order 40: the databases a repository defines in .sql files (spec section 6, "Databases from repositories").
/// Files are parsed, never run. Scripts are applied in natural file-name order, so what is recorded is the final schema.
/// </summary>
public sealed class SqlSchemaExtractor : IExtractor
{
    public int Order => 40;

    sealed record SqlFile(string Path, SqlScript Script, string? Project);

    public void Extract(Extraction x)
    {
        var projects = x.Repo.WithExtension(".sqlproj").ToList();
        // Digit runs are padded so that 2_x.sql sorts before 10_x.sql.
        var files = x.Repo.WithExtension(".sql").OrderBy(f => Regex.Replace(f, @"\d+", m => m.Value.PadLeft(10, '0')), StringComparer.Ordinal).ThenBy(f => f, StringComparer.Ordinal)
            .Select(f => new SqlFile(f, Parse(x.Repo, f), projects.Where(p => Under(f, Directory(p))).MaxBy(p => Directory(p).Length))).ToList();

        // Which database each file is for: its project's; else the one it names; else the one its directory's files agree on; else the fallback.
        var loose = files.Where(f => f.Project is null).ToList();
        var declared = loose.Where(f => f.Script.Database is not null).GroupBy(f => Directory(f.Path))
            .ToDictionary(g => g.Key, g => g.Select(f => f.Script.Database!).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        string? Named(SqlFile f) => f.Project is not null ? Path.GetFileNameWithoutExtension(f.Project)
            : f.Script.Database ?? (declared.GetValueOrDefault(Directory(f.Path)) is { Count: 1 } names ? names[0] : null);
        var unnamed = loose.Where(f => Named(f) is null && f.Script.Changes.Count > 0).Select(f => f.Path).ToList();
        var fallback = unnamed.Count == 0 ? null : CommonDirectory(unnamed) is { Length: > 0 } dir ? dir.Split('/')[^1] : x.Repo.Repository;

        foreach (var group in files.Select(f => (File: f, Name: Named(f) ?? fallback)).Where(f => f.Name is not null)
                     .GroupBy(f => Ids.Database(f.Name!), f => f, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var defining = group.Where(f => f.File.Script.Changes.Count > 0).Select(f => f.File.Path).ToList();
            if (defining.Count == 0) continue;
            var source = group.First().File.Project ?? (CommonDirectory(defining) is { Length: > 0 } folder ? folder : defining[0]);
            Database(x, group.First().Name!, source, group.Select(f => f.File).ToList());
        }
    }

    static SqlScript Parse(Checkout repo, string path)
    {
        try { return SqlParser.Parse(repo.Read(path)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var unread = new SqlScript();
            unread.Errors.Add(new(0, 0, e.Message));
            return unread;
        }
    }

    static string Directory(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    static bool Under(string path, string directory) => directory == "" || path.StartsWith(directory + "/", StringComparison.Ordinal);

    static string CommonDirectory(IEnumerable<string> paths)
    {
        var common = paths.Select(p => Directory(p).Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Aggregate((a, b) => a.TakeWhile((segment, i) => i < b.Length && b[i] == segment).ToArray());
        return string.Join('/', common);
    }

    static void Database(Extraction x, string name, string source, List<SqlFile> files)
    {
        var schema = new SqlSchema();
        foreach (var f in files) schema.Apply(f.Script, f.Path);
        var db = x.Database(name, source);
        var modules = schema.Views.Values.Concat<SqlModule>(schema.Routines.Values).Concat(schema.Triggers.Values).ToList();
        db.Extracted = Counts(schema) +
            $"- SQL files: {files.Count} read, {files.Count(f => f.Script.Errors.Count > 0)} with parse errors, {files.Count(f => f.Script.Changes.Count == 0)} without definitions\n" +
            $"- Dynamic SQL statements, not followed: {files.Sum(f => f.Script.DynamicSql) + modules.Sum(m => m.DynamicSql)}\n";
        Objects(name, schema,
            (id, kind, objectName, summary) => x.Get(id, kind, objectName, () => summary),
            (e, file, line) => x.Cite(e, "sql", file, line),
            m => $"Defined in `{m.File}`, lines {m.Line}-{m.EndLine}.\n");
    }

    public static string Counts(SqlSchema schema) =>
        $"- Tables: {schema.Tables.Count}\n- Views: {schema.Views.Count}\n" +
        $"- Procedures: {schema.Routines.Values.Count(r => !r.IsFunction)}\n- Functions: {schema.Routines.Values.Count(r => r.IsFunction)}\n- Triggers: {schema.Triggers.Count}\n";

    /// <summary>
    /// The table, view, routine and trigger entries of one database. Shared with the live reader (Db/LiveDb.cs), so that a
    /// schema read from a repository and from an instance gives the same IDs, names, summaries, links and extracted blocks.
    /// </summary>
    /// <param name="start">Makes the entry: ID, kind, name, summary.</param>
    /// <param name="cite">Declares the source of a fact on an entry and returns its citation: entry, file, line.</param>
    /// <param name="where">The sentence saying where a module is defined.</param>
    public static void Objects(string name, SqlSchema schema, Func<string, string, string, string, Entry> start, Func<Entry, string, int?, string> cite, Func<SqlModule, string> where)
    {
        string Id(string folder, SqlName n) => Ids.DbObject(name, folder, n.Schema, n.Name);
        bool Here(SqlName n) => n.Database is null || n.Database.Equals(name, StringComparison.OrdinalIgnoreCase);
        string? TableId(SqlName n) => Here(n) && (schema.Tables.ContainsKey(n.Key) || schema.Views.ContainsKey(n.Key)) ? Id("tables", n) : null;
        string? RoutineId(SqlName n) => Here(n) && schema.Routines.ContainsKey(n.Key) ? Id("routines", n) : null;
        Entry Start(SqlObject o, string folder, string kind, string what)
        {
            var e = start(Id(folder, o.Name), kind, o.Name.ToString(), $"{what} in {name}; not yet described.");
            cite(e, o.File, null);
            return e;
        }
        // A target defined here gets 'to'; anything else is left for the resolver, by name.
        void Link(Entry e, string type, SqlName target, string? to, string file, int line, string? constraint = null) => Extraction.AddLink(e, new Link
        {
            Type = type, To = to, Name = to is null ? target.ToString() : constraint, Database = to is null ? target.Database : null,
            How = "sql", Basis = Basis.Extracted, Source = cite(e, file, line),
        });
        void Body(Entry e, SqlModule m)
        {
            foreach (var r in m.Reads) Link(e, "reads", r.Name, TableId(r.Name), m.File, r.Line);
            foreach (var r in m.Writes) Link(e, "writes", r.Name, TableId(r.Name), m.File, r.Line);
            foreach (var r in m.Calls) Link(e, "calls", r.Name, RoutineId(r.Name), m.File, r.Line);
        }
        string Defined(SqlModule m) => where(m) +
            (m.DynamicSql > 0 ? $"Runs dynamic SQL in {m.DynamicSql} place{(m.DynamicSql == 1 ? "" : "s")}; what that touches is not recorded.\n" : "");

        foreach (var t in schema.Tables.Values)
        {
            var e = Start(t, "tables", Kinds.Table, $"Table {t.Name}");
            e.Extracted = DbText.Columns(t.DbTextColumns());
            foreach (var fk in t.ForeignKeys) Link(e, "references", fk.References, Here(fk.References) && schema.Tables.ContainsKey(fk.References.Key) ? Id("tables", fk.References) : null, fk.File, fk.Line, fk.Name);
        }
        foreach (var v in schema.Views.Values)
        {
            var e = Start(v, "tables", Kinds.View, $"View {v.Name}");
            e.Extracted = Defined(v);
            Body(e, v);
        }
        foreach (var r in schema.Routines.Values)
        {
            var e = Start(r, "routines", Kinds.Routine, $"{(r.IsFunction ? "Function" : "Procedure")} {r.Name}");
            e.Type = r.IsFunction ? "function" : "procedure";
            e.Extracted = DbText.Parameters(r.Parameters) + "\n" + Defined(r);
            Body(e, r);
        }
        foreach (var g in schema.Triggers.Values)
        {
            var e = Start(g, "triggers", Kinds.Trigger, $"Trigger {g.Name} on {g.Table}");
            e.Extracted = $"Fires {g.Timing} {string.Join(", ", g.Events)} on {g.Table}.\n\n" + Defined(g);
            Link(e, "fires-on", g.Table, TableId(g.Table), g.File, g.Line);
            Body(e, g);
        }
    }
}
