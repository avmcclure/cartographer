using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Cartographer;

/// <summary>databases/&lt;database&gt;/scan-state.yml: what each live instance last showed. Written only by the CLI.</summary>
public sealed class DbScanState
{
    const string Header = "# Written by the CLI. Do not edit by hand. Git history of this file is the log of live reads.\n";

    /// <summary>The database entry's ID.</summary>
    public string Database { get; set; } = "";
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string Cartographer { get; set; } = Clock.Version;
    /// <summary>Instance name to its last read.</summary>
    public SortedDictionary<string, InstanceRead> Instances { get; set; } = new(StringComparer.Ordinal);

    static string PathOf(Atlas atlas, string databaseId) => Path.Combine(atlas.Root, Ids.Area(databaseId)!.Replace('/', Path.DirectorySeparatorChar), ScanState.FileName);

    public static DbScanState Load(Atlas atlas, string databaseId)
    {
        var path = PathOf(atlas, databaseId);
        return (File.Exists(path) ? EntryFile.Reader.Deserialize<DbScanState>(File.ReadAllText(path)) : null) ?? new DbScanState { Database = databaseId };
    }

    public void Save(Atlas atlas)
    {
        var path = PathOf(atlas, Database);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Header + EntryFile.Yaml.Serialize(this));
    }
}

public sealed class InstanceRead
{
    /// <summary>DbCatalog.Fingerprint at the last successful read.</summary>
    public string? Fingerprint { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Read { get; set; }
    public string? Permission { get; set; }
    /// <summary>What that read could not see, in words; kept because an unchanged database is not read in full again.</summary>
    public string? NotRead { get; set; }
    /// <summary>Set by a failed read, cleared by the next successful one.</summary>
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)] public string? Failed { get; set; }
    public string? Reason { get; set; }
    /// <summary>Entries the last successful read produced.</summary>
    public List<string> Entries { get; set; } = [];
}

/// <summary>
/// Reading schema metadata from a live SQL Server instance (spec sections 6 and 7, "db read").
/// Catalog rows (DbCatalog) become the same SqlSchema the repository parser builds, and from there the same entries.
/// </summary>
public static class LiveDb
{
    const string SourceQuestion = "Where the source of database ";

    // ---- which instances, and how to connect ----

    /// <summary>Why an instance may not be read, or null when it may.</summary>
    public static string? Refusal(InstanceConfig i) =>
        i.Allowed == false ? $"instance '{i.Name}' is marked allowed: false in {Atlas.ConfigFile}; it is not read"
        : i.Production && i.Allowed != true ? $"instance '{i.Name}' is a production instance; it is read only when {Atlas.ConfigFile} also says allowed: true for it"
        : null;

    public static string EnvironmentVariable(string instanceName) => "CARTOGRAPHER_SQL_" + Regex.Replace(instanceName.ToUpperInvariant(), "[^A-Z0-9]", "_");

    /// <summary>The supplied connection string, or integrated sign-in to the configured server; always with our name, short timeouts and read-only intent.</summary>
    public static string ConnectionString(InstanceConfig i, string? supplied)
    {
        // TrustServerCertificate stays false unless the supplied string asks for it.
        var b = supplied is null ? new SqlConnectionStringBuilder { DataSource = i.Server, IntegratedSecurity = true } : new SqlConnectionStringBuilder(supplied);
        b.ApplicationName = "Cartographer";
        b.ApplicationIntent = ApplicationIntent.ReadOnly;
        b.ConnectTimeout = 15;
        b.CommandTimeout = 30;
        b.ConnectRetryCount = 0;
        b.PersistSecurityInfo = false;
        return b.ConnectionString;
    }

    // ---- catalog rows to the parser's model ----

    /// <summary>A catalog type in DbText.TypeName form: "nvarchar(50)", "decimal(18,2)", "varchar(max)", "int".</summary>
    public static string TypeName(string type, int maxLength, int precision, int scale)
    {
        var name = DbText.TypeName(type);
        return name switch
        {
            "char" or "varchar" or "binary" or "varbinary" => $"{name}({(maxLength < 0 ? "max" : maxLength)})",
            "nchar" or "nvarchar" => $"{name}({(maxLength < 0 ? "max" : maxLength / 2)})",
            "decimal" or "numeric" => $"{name}({precision},{scale})",
            "datetime2" or "time" or "datetimeoffset" => $"{name}({scale})",
            _ => name,
        };
    }

    public static SqlSchema Schema(DbCatalog c)
    {
        static string Key(string schema, string name) => $"{schema}.{name}".ToLowerInvariant();
        var schema = new SqlSchema();
        var definitions = new Dictionary<string, string?>();
        foreach (var m in c.Modules) definitions[Key(m.Schema, m.Name)] = m.Definition;
        var columns = c.Columns.ToLookup(x => Key(x.Schema, x.Table));
        var keys = c.ForeignKeys.ToLookup(x => Key(x.Schema, x.Table));
        var parameters = c.Parameters.ToLookup(x => Key(x.Schema, x.Routine));
        var triggers = c.Triggers.ToLookup(x => Key(x.Schema, x.Name));

        // The parser gives what a body reads, writes and calls; names, parameters and trigger events come from the catalog, which a rename keeps right.
        T Module<T>(DbObjectRow o, Func<SqlScript, IEnumerable<T>> pick) where T : SqlModule, new()
        {
            var text = definitions.GetValueOrDefault(Key(o.Schema, o.Name));
            var module = (text is null ? null : pick(SqlParser.Parse(text)).FirstOrDefault()) ?? new T();
            module.Name = new SqlName(null, o.Schema, o.Name);
            return module;
        }

        foreach (var o in c.Objects)
        {
            var key = Key(o.Schema, o.Name);
            switch (o.Type)
            {
                case "U":
                    var table = new SqlTable { Name = new SqlName(null, o.Schema, o.Name) };
                    // A computed column is "computed" and nullable in the repository form, which cannot work its type out.
                    foreach (var x in columns[key])
                        table.Columns.Add(new SqlColumn { Name = x.Name, Type = x.Computed ? "computed" : x.Type, Nullable = x.Computed || x.Nullable, PrimaryKey = x.PrimaryKey });
                    foreach (var fk in keys[key].GroupBy(x => x.Name))
                        table.ForeignKeys.Add(new SqlForeignKey
                        {
                            // A key the repository left unnamed has a generated name here; neither side names it.
                            Name = fk.First().SystemNamed ? null : fk.Key, Columns = fk.Select(x => x.Column).ToList(),
                            References = new SqlName(null, fk.First().ReferencedSchema, fk.First().ReferencedTable),
                        });
                    schema.Tables[key] = table;
                    break;
                case "V":
                    schema.Views[key] = Module(o, s => s.Views);
                    break;
                case "P" or "FN" or "IF" or "TF":
                    var routine = Module(o, s => s.Routines);
                    routine.IsFunction = o.Type != "P";
                    routine.Parameters.Clear();
                    routine.Parameters.AddRange(parameters[key].Select(p => new Parameter(p.Name, p.Type, p.Output)));
                    schema.Routines[key] = routine;
                    break;
                case "TR" when triggers[key].ToList() is { Count: > 0 } events:
                    var trigger = Module(o, s => s.Triggers);
                    trigger.Table = new SqlName(null, o.Schema, events[0].Table);
                    trigger.Timing = events[0].InsteadOf ? "instead of" : "after";
                    trigger.Events.Clear();
                    trigger.Events.AddRange(events.Select(e => e.Event));
                    schema.Triggers[key] = trigger;
                    break;
            }
        }
        return schema;
    }

    // ---- what the read was allowed to see ----

    static List<DbModuleRow> Unreadable(DbCatalog c) => c.Modules.Where(m => m.Definition is null && !m.Encrypted).ToList();

    /// <summary>"limited" when objects or definitions may be hidden, "full" when the dependency catalog is readable too, otherwise "definitions".</summary>
    public static string Permission(DbCatalog c) => !c.ViewDefinition || Unreadable(c).Count > 0 ? "limited" : c.Dependencies ? "full" : "definitions";

    /// <summary>What a read could not see, in words for the database entry; empty when nothing.</summary>
    public static string NotRead(DbCatalog c)
    {
        var parts = new List<string>();
        if (!c.ViewDefinition) parts.Add("the identity lacks VIEW DEFINITION on the database, so objects it has no permission on are not listed and absence here does not mean none exist");
        if (Unreadable(c).Count > 0) parts.Add($"the definitions of {Unreadable(c).Count} of {c.Modules.Count} views, routines and triggers, so what those read, write and call");
        if (c.Modules.Count(m => m.Encrypted) is > 0 and var encrypted) parts.Add($"{encrypted} encrypted definition{(encrypted == 1 ? "" : "s")}");
        if (!c.Dependencies) parts.Add("the dependency catalog (sys.sql_expression_dependencies)");
        return string.Join("; ", parts);
    }

    // ---- entries ----

    static string SourceKey(string instance) => "live-" + Regex.Replace(Ids.Slug(instance), "[^a-z0-9]+", "-").Trim('-');

    /// <summary>The entries one database gives, as SqlSchemaExtractor would give them for the same schema; the database entry first.</summary>
    public static List<Entry> Entries(DbCatalog c, InstanceConfig instance)
    {
        var schema = Schema(c);
        var key = SourceKey(instance.Name);
        var pointer = $"instance:{instance.Name}@{Clock.Today}";
        var modules = schema.Views.Values.Concat<SqlModule>(schema.Routines.Values).Concat(schema.Triggers.Values);
        var db = new Entry
        {
            Id = Ids.Database(c.Database), Kind = Kinds.Database, Name = c.Database, Engine = "sqlserver",
            Summary = Short($"Database {c.Database}, found on instance {instance.Name}; not yet described."),
            Instances = [new Instance { Name = instance.Name, Environment = instance.Environment.Length > 0 ? instance.Environment : null, Read = Clock.Today, Permission = Permission(c) }],
            Extracted = SqlSchemaExtractor.Counts(schema) + $"- Dynamic SQL statements, not followed: {modules.Sum(m => m.DynamicSql)}\n",
        };
        db.Source(key, pointer);
        var entries = new List<Entry> { db };
        var hidden = c.Modules.Where(m => m.Definition is null).Select(m => $"{m.Schema}.{m.Name}".ToLowerInvariant()).ToHashSet();
        SqlSchemaExtractor.Objects(c.Database, schema,
            (id, kind, name, summary) =>
            {
                var e = new Entry { Id = id, Kind = kind, Name = name, Summary = Short(summary), Database = db.Id };
                entries.Add(e);
                return e;
            },
            (e, _, _) => e.Source(key, pointer),
            m => hidden.Contains(m.Name.Key)
                ? $"Its definition could not be read on instance {instance.Name}, so what it reads, writes and calls is not known from there.\n"
                : $"Definition read from instance {instance.Name}.\n");
        foreach (var e in entries) e.Verified = new Verified { Date = Clock.Today, Cartographer = Clock.Version };
        return entries;
    }

    static string Short(string s) => s.Length <= 200 ? s : s[..199] + "…";

    static List<string> Repositories(Entry e) => (e.Sources ?? []).Values.Select(Evidence.OriginOf)
        .Where(o => o is not null && o != "person" && !o.StartsWith("instance:")).Select(o => o!).Distinct().Order(StringComparer.Ordinal).ToList();

    /// <summary>The fingerprint the last read of this database recorded, or null when it must be rebuilt anyway.</summary>
    public static string? KnownFingerprint(Atlas atlas, InstanceConfig instance, string database) =>
        atlas.Find(Ids.Database(database)) is null ? null : DbScanState.Load(atlas, Ids.Database(database)).Instances.GetValueOrDefault(instance.Name)?.Fingerprint;

    /// <summary>Merges one database's catalog into the atlas and records the read. Does not save entries; returns one line saying what happened.</summary>
    public static string Apply(Atlas atlas, InstanceConfig instance, DbCatalog c)
    {
        var origin = "instance:" + instance.Name;
        var dbId = Ids.Database(c.Database);
        var area = Ids.Area(dbId) + "/";
        var state = DbScanState.Load(atlas, dbId);
        var unchanged = c.Fingerprint == KnownFingerprint(atlas, instance, c.Database);
        var read = state.Instances.GetValueOrDefault(instance.Name) ?? new InstanceRead();
        state.Instances[instance.Name] = read;
        // What repositories define, taken before the merge revives anything a repository has since dropped.
        var inRepository = atlas.Entries.Values.Where(e => e.Id.StartsWith(area) && e.Kind != Kinds.Database && e.Status is null && Repositories(e).Count > 0)
            .ToDictionary(e => e.Id, e => $"{e.Kind} {e.Name}");

        if (!unchanged)
        {
            var entries = Entries(c, instance);
            Merger.Apply(atlas, origin, entries, read.Entries);
            // An object only this instance knew, now gone from it. Not when objects may merely be hidden.
            if (c.ViewDefinition)
                foreach (var id in read.Entries.Except(entries.Select(e => e.Id)))
                    if (atlas.Find(id) is { } gone && (gone.Sources ?? []).Values.All(v => Evidence.OriginOf(v) is "person" or null)) gone.Status = "retired";
            read.Entries = entries.Select(e => e.Id).Order(StringComparer.Ordinal).ToList();
            (read.Fingerprint, read.Permission, read.NotRead) = (c.Fingerprint, Permission(c), NotRead(c) is { Length: > 0 } not ? not : null);
        }

        var db = atlas.Entries[dbId];
        // An unchanged database was not read in full, so its permission level is the one its last full read found.
        var permission = read.Permission ?? Permission(c);
        var previous = read.Read;
        (read.Read, read.Failed, read.Reason) = (Clock.Today, null, null);
        db.Instances = (db.Instances ?? []).Where(i => i.Name != instance.Name)
            .Append(new Instance { Name = instance.Name, Environment = instance.Environment.Length > 0 ? instance.Environment : null, Read = Clock.Today, Permission = permission })
            .OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        Note(db, instance.Name, $": permission {permission}." + (read.NotRead is null ? "" : $" Not read: {read.NotRead}."));

        // Existence drift: what the repository defines and the instance lacks, and the reverse.
        var repositories = Repositories(db);
        db.Drift?.RemoveAll(d => d.Sources.Contains(origin));
        if (repositories.Count > 0)
        {
            static string Folder(string type) => type is "U" or "V" ? "tables" : type == "TR" ? "triggers" : "routines";
            static string Kind(string type) => type switch { "U" => "table", "V" => "view", "TR" => "trigger", _ => "routine" };
            static string List(IEnumerable<string> names) => names.Order(StringComparer.Ordinal).ToList() is var all && all.Count > 20
                ? $"{string.Join(", ", all.Take(20))} and {all.Count - 20} more" : string.Join(", ", all);
            var live = new Dictionary<string, string>();
            foreach (var o in c.Objects) live[Ids.DbObject(c.Database, Folder(o.Type), o.Schema, o.Name)] = $"{Kind(o.Type)} {o.Schema}.{o.Name}";
            // Without VIEW DEFINITION an object the instance does not show may only be hidden.
            var missing = c.ViewDefinition ? inRepository.Where(r => !live.ContainsKey(r.Key)).Select(r => r.Value).ToList() : [];
            var extra = live.Where(l => !inRepository.ContainsKey(l.Key)).Select(l => l.Value).ToList();
            var what = new List<string>();
            if (missing.Count > 0) what.Add($"defined in the repository but not found on {instance.Name}: {List(missing)}");
            if (extra.Count > 0) what.Add($"found on {instance.Name} but not defined in the repository: {List(extra)}");
            if (what.Count > 0) (db.Drift ??= []).Add(new Drift { What = string.Join("; ", what), Sources = [.. repositories, origin] });
        }

        // A database no repository defines: ask where its source lives, once; withdraw the question when one turns up.
        QuestionList.Update(db, list =>
        {
            if (repositories.Count > 0) list.Items.RemoveAll(q => q.RaisedBy == "cli" && q.State is "open" or "needs-person" && q.Missing.StartsWith(SourceQuestion));
            else if (!list.Dismissed.Any(d => d.Contains("source", StringComparison.OrdinalIgnoreCase)))
                list.Add($"{SourceQuestion}{c.Database} lives. It was found only on instance {instance.Name}; no scanned repository defines it.", "person",
                    "A repository that defines its schema, or a person stating that it is not in source control.", "cli");
        });
        state.Save(atlas);
        return unchanged
            ? $"{c.Database}: unchanged since the read of {previous}; not rebuilt"
            : $"{c.Database}: {read.Entries.Count} entries read, permission {permission}";
    }

    /// <summary>Records a failed read on the databases already known for the instance (or the one named). Nothing learned earlier is removed.</summary>
    public static void Failed(Atlas atlas, InstanceConfig instance, string? database, string reason)
    {
        foreach (var db in atlas.OfKind(Kinds.Database).Where(d => d.Instances?.Any(i => i.Name == instance.Name) == true && (database is null || d.Id == Ids.Database(database))).ToList())
        {
            var last = db.Instances!.First(i => i.Name == instance.Name).Read;
            Note(db, instance.Name, $" failed on {Clock.Today}: {reason}. What is recorded is from the read of {last}.");
            var state = DbScanState.Load(atlas, db.Id);
            if (!state.Instances.TryGetValue(instance.Name, out var read)) state.Instances[instance.Name] = read = new InstanceRead();
            (read.Failed, read.Reason) = (Clock.Today, reason);
            state.Save(atlas);
        }
    }

    /// <summary>
    /// Sets this instance's line in the database entry's extracted block. A later repository scan rewrites that block
    /// without the line (Merger owns the block); the next live read puts it back, and `instances` keeps the permission meanwhile.
    /// </summary>
    static void Note(Entry db, string instance, string text)
    {
        var prefix = $"- Live read of {instance}";
        var block = Regex.Match(Sections.Get(db.Body, Sections.Technical), @"<!-- extracted -->\n(.*?)\n?<!-- /extracted -->", RegexOptions.Singleline).Groups[1].Value;
        var lines = block.Split('\n').Where(l => l.Length > 0 && !l.StartsWith(prefix + ":") && !l.StartsWith(prefix + " failed")).Append(prefix + text);
        db.Body = Sections.SetExtracted(db.Body, string.Join('\n', lines));
    }

    // ---- the read itself ----

    /// <summary>A short reason that never repeats server messages, which can quote parts of a connection string.</summary>
    static string Reason(Exception e) => e is SqlException s
        ? s.Number switch
        {
            18456 or 18452 => "login failed",
            4060 or 911 or 916 => "the database does not exist or this identity may not open it",
            -2 => "timed out",
            _ => $"unreachable or refused (SQL Server error {s.Number})",
        }
        : $"could not connect ({e.GetType().Name})";

    /// <summary>`db read`: reads the listed instance into the atlas, resolves and saves. Returns the exit code and what to print.</summary>
    /// <param name="connectionString">Overrides the environment variable; tests use it.</param>
    public static (int Code, string Text) Read(Atlas atlas, string instanceName, string? database = null, string? connectionString = null)
    {
        var instance = atlas.Config.Instances.FirstOrDefault(i => i.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException($"instance '{instanceName}' is not listed under instances in {Atlas.ConfigFile}; only listed instances are read" +
                (atlas.Config.Instances.Count > 0 ? $". Listed: {string.Join(", ", atlas.Config.Instances.Select(i => i.Name))}" : ""));
        if (Refusal(instance) is { } refusal) throw new UsageException(refusal);
        var variable = EnvironmentVariable(instance.Name);
        connectionString ??= Environment.GetEnvironmentVariable(variable);
        string full;
        try { full = ConnectionString(instance, connectionString); }
        catch (Exception e) when (e is ArgumentException or FormatException or KeyNotFoundException) { throw new UsageException($"the connection string in {variable} is not valid"); }

        string[] system = ["master", "model", "msdb", "tempdb"];
        var text = new StringBuilder();
        var failed = false;
        void Fail(string? name, Exception e)
        {
            failed = true;
            Failed(atlas, instance, name, Reason(e));
            text.AppendLine($"{name ?? instance.Name}: read failed: {Reason(e)}");
        }
        try
        {
            using var connection = new SqlConnection(full);
            connection.Open();
            var names = (instance.Databases.Count > 0 ? instance.Databases : CatalogReader.Databases(connection))
                .Where(n => !system.Contains(n, StringComparer.OrdinalIgnoreCase) && (database is null || n.Equals(database, StringComparison.OrdinalIgnoreCase)))
                .Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (database is not null && names.Count == 0) throw new UsageException($"'{database}' is not a database {Atlas.ConfigFile} lets instance {instance.Name} be read for");
            foreach (var name in names)
            {
                try { text.AppendLine(Apply(atlas, instance, CatalogReader.Read(connection, name, n => KnownFingerprint(atlas, instance, n)))); }
                catch (Exception e) when (e is SqlException or InvalidOperationException) { Fail(name, e); }
            }
        }
        catch (Exception e) when (e is not UsageException) { Fail(database, e); }
        Resolver.Run(atlas);
        atlas.Save();
        return (failed ? 1 : 0, text.ToString());
    }
}
