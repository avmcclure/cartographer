using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Cartographer;

/// <summary>A schema-qualified name. Schema is "dbo" when the source gave none; Database is set only by a three-part name.</summary>
public sealed record SqlName(string? Database, string Schema, string Name)
{
    /// <summary>"schema.name" in lower case: what objects are keyed by inside one database.</summary>
    public string Key => $"{Schema}.{Name}".ToLowerInvariant();
    public override string ToString() => $"{Schema}.{Name}";
}

/// <summary>A table read or written, or a routine called, and the line that does it.</summary>
public sealed record SqlRef(SqlName Name, int Line);
public sealed record SqlError(int Line, int Column, string Message);

public sealed class SqlColumn
{
    public string Name { get; set; } = "";
    /// <summary>In DbText.TypeName form; "computed" for a computed column.</summary>
    public string Type { get; set; } = "";
    public bool Nullable { get; set; }
    public bool PrimaryKey { get; set; }
}

public sealed class SqlForeignKey
{
    public string? Name { get; set; }
    public List<string> Columns { get; set; } = [];
    public SqlName References { get; set; } = null!;
    /// <summary>Set by SqlSchema.Apply; an ALTER TABLE can add a key from another file than the table's.</summary>
    public string File { get; set; } = "";
    public int Line { get; set; }
}

public abstract class SqlObject
{
    public SqlName Name { get; set; } = null!;
    /// <summary>Set by SqlSchema.Apply.</summary>
    public string File { get; set; } = "";
    public int Line { get; set; }
    public int EndLine { get; set; }
}

public sealed class SqlTable : SqlObject
{
    public List<SqlColumn> Columns { get; } = [];
    public List<SqlForeignKey> ForeignKeys { get; } = [];
    public string? PrimaryKeyName { get; set; }
    /// <summary>Columns named by a table-level PRIMARY KEY constraint; an ALTER TABLE names columns the table already has.</summary>
    public List<string> PrimaryKeyColumns { get; } = [];

    public IEnumerable<Column> DbTextColumns() => Columns.Select(c =>
    {
        var fk = ForeignKeys.Any(f => f.Columns.Contains(c.Name, StringComparer.OrdinalIgnoreCase));
        return new Column(c.Name, c.Type, c.Nullable, c.PrimaryKey ? fk ? "PK, FK" : "PK" : fk ? "FK" : "");
    });
}

/// <summary>ALTER TABLE: what it adds (columns, keys), redefines (ALTER COLUMN) and drops.</summary>
public sealed class SqlAlterTable
{
    public SqlTable Adds { get; set; } = null!;
    public List<SqlColumn> Alters { get; } = [];
    public List<string> DropColumns { get; } = [];
    public List<string> DropConstraints { get; } = [];
}

public enum SqlKind { Table, View, Routine, Trigger }
public sealed record SqlDrop(SqlKind Kind, SqlName Name);

/// <summary>Something with a body: what the body reads, writes and calls. Each list names a target once, at its first line.</summary>
public abstract class SqlModule : SqlObject
{
    public List<SqlRef> Reads { get; set; } = [];
    public List<SqlRef> Writes { get; set; } = [];
    public List<SqlRef> Calls { get; set; } = [];
    /// <summary>EXEC of a string or variable, and sp_executesql: what they touch is not read.</summary>
    public int DynamicSql { get; set; }
}

public sealed class SqlView : SqlModule;

public sealed class SqlRoutine : SqlModule
{
    public bool IsFunction { get; set; }
    public List<Parameter> Parameters { get; } = [];
}

public sealed class SqlTrigger : SqlModule
{
    public SqlName Table { get; set; } = null!;
    /// <summary>"after" or "instead of".</summary>
    public string Timing { get; set; } = "after";
    /// <summary>"insert", "update", "delete", in the order written.</summary>
    public List<string> Events { get; } = [];
}

/// <summary>What one piece of T-SQL text declares, in the order it declares it.</summary>
public sealed class SqlScript
{
    /// <summary>SqlTable, SqlView, SqlRoutine, SqlTrigger (create or replace), SqlAlterTable and SqlDrop, in source order.</summary>
    public List<object> Changes { get; } = [];
    public List<SqlError> Errors { get; } = [];
    /// <summary>The USE in effect at the first change (the last USE when there are no changes), else the CREATE DATABASE name. System databases are ignored.</summary>
    public string? Database { get; set; }
    /// <summary>Dynamic SQL outside any routine, trigger or view.</summary>
    public int DynamicSql { get; set; }

    public IEnumerable<SqlTable> Tables => Changes.OfType<SqlTable>();
    public IEnumerable<SqlView> Views => Changes.OfType<SqlView>();
    public IEnumerable<SqlRoutine> Routines => Changes.OfType<SqlRoutine>();
    public IEnumerable<SqlTrigger> Triggers => Changes.OfType<SqlTrigger>();
}

/// <summary>The schema that scripts add up to when applied in order. Keys are SqlName.Key.</summary>
public sealed class SqlSchema
{
    public SortedDictionary<string, SqlTable> Tables { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, SqlView> Views { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, SqlRoutine> Routines { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, SqlTrigger> Triggers { get; } = new(StringComparer.Ordinal);

    public void Apply(SqlScript script, string file = "")
    {
        foreach (var change in script.Changes)
        {
            if (change is SqlObject o) o.File = file;
            switch (change)
            {
                case SqlTable t: foreach (var f in t.ForeignKeys) f.File = file; Tables[t.Name.Key] = t; break;
                case SqlView v: Views[v.Name.Key] = v; break;
                case SqlRoutine r: Routines[r.Name.Key] = r; break;
                case SqlTrigger g: Triggers[g.Name.Key] = g; break;
                case SqlDrop d:
                    if (d.Kind == SqlKind.Table) { Tables.Remove(d.Name.Key); foreach (var g in Triggers.Values.Where(g => g.Table.Key == d.Name.Key).ToList()) Triggers.Remove(g.Name.Key); }
                    else if (d.Kind == SqlKind.View) Views.Remove(d.Name.Key);
                    else if (d.Kind == SqlKind.Routine) Routines.Remove(d.Name.Key);
                    else Triggers.Remove(d.Name.Key);
                    break;
                case SqlAlterTable a when Tables.TryGetValue(a.Adds.Name.Key, out var t): Alter(t, a, file); break;
            }
        }
    }

    static void Alter(SqlTable t, SqlAlterTable a, string file)
    {
        SqlColumn? Find(string name) => t.Columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        foreach (var c in a.Adds.Columns) if (Find(c.Name) is null) t.Columns.Add(c);
        // A primary key added as a table constraint names columns the table already has.
        foreach (var c in a.Adds.PrimaryKeyColumns) if (Find(c) is { } column) { column.PrimaryKey = true; column.Nullable = false; }
        t.PrimaryKeyName = a.Adds.PrimaryKeyName ?? t.PrimaryKeyName;
        foreach (var f in a.Adds.ForeignKeys) { f.File = file; t.ForeignKeys.Add(f); }
        foreach (var c in a.Alters) if (Find(c.Name) is { } column) { column.Type = c.Type; column.Nullable = c.Nullable && !column.PrimaryKey; }
        foreach (var name in a.DropColumns) t.Columns.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        foreach (var name in a.DropConstraints)
        {
            t.ForeignKeys.RemoveAll(f => name.Equals(f.Name, StringComparison.OrdinalIgnoreCase));
            if (name.Equals(t.PrimaryKeyName, StringComparison.OrdinalIgnoreCase)) { t.PrimaryKeyName = null; foreach (var c in t.Columns) c.PrimaryKey = false; }
        }
    }
}

public static class SqlParser
{
    /// <summary>
    /// Reads T-SQL text (any number of statements and GO batches) into what it declares. Never throws on bad SQL:
    /// problems are in Errors, and whatever did parse is still in Changes.
    /// </summary>
    public static SqlScript Parse(string sql)
    {
        var script = new SqlScript();
        try
        {
            var tree = new TSql180Parser(initialQuotedIdentifiers: true).Parse(new StringReader(sql), out var errors);
            foreach (var e in errors) script.Errors.Add(new(e.Line, e.Column, e.Message));
            tree?.Accept(new ScriptVisitor(script));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            script.Errors.Add(new(0, 0, e.Message));
        }
        return script;
    }

    static readonly string[] SystemDatabases = ["master", "tempdb", "model", "msdb"];
    // ponytail: a method call on a column (xml, hierarchyid, spatial) looks like schema.function(); these names are taken as methods. A known-schema check if that proves too loose.
    static readonly string[] Methods = ["value", "query", "exist", "nodes", "modify", "ToString", "GetAncestor", "GetLevel", "IsDescendantOf", "GetDescendant", "GetReparentedValue", "Parse", "Point"];

    static SqlName Name(SchemaObjectName n) => new(n.DatabaseIdentifier?.Value, n.SchemaIdentifier?.Value is { Length: > 0 } s ? s : "dbo", n.BaseIdentifier?.Value ?? "");

    static bool Temporary(SchemaObjectName n) => n.BaseIdentifier?.Value is null or "" || n.BaseIdentifier.Value[0] is '#' or '@';

    static string Text(TSqlFragment f) => string.Concat(Enumerable.Range(f.FirstTokenIndex, f.LastTokenIndex - f.FirstTokenIndex + 1)
        .Select(i => f.ScriptTokenStream[i]).Where(t => t.TokenType is not (TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment)).Select(t => t.Text));

    static T Located<T>(T o, TSqlFragment f, SqlName name) where T : SqlObject
    {
        o.Name = name;
        o.Line = f.StartLine;
        o.EndLine = f.LastTokenIndex >= 0 ? f.ScriptTokenStream[f.LastTokenIndex].Line : f.StartLine;
        return o;
    }

    /// <summary>Walks a script for declarations, into IF and BEGIN blocks but not into the bodies of routines, triggers and views.</summary>
    sealed class ScriptVisitor(SqlScript script) : TSqlFragmentVisitor
    {
        string? _use, _created;
        int _moduleEnd = -1;

        bool Inside(TSqlFragment n) => n.FirstTokenIndex <= _moduleEnd;

        void Add(object change)
        {
            if (script.Changes.Count == 0) script.Database = _use;
            script.Changes.Add(change);
        }

        public override void ExplicitVisit(TSqlScript node)
        {
            base.ExplicitVisit(node);
            script.Database ??= (script.Changes.Count == 0 ? _use : null) ?? _created;
        }

        public override void Visit(UseStatement node)
        {
            if (!SystemDatabases.Contains(node.DatabaseName.Value, StringComparer.OrdinalIgnoreCase)) _use = node.DatabaseName.Value;
        }

        public override void Visit(CreateDatabaseStatement node) => _created ??= node.DatabaseName.Value;

        public override void Visit(CreateTableStatement node)
        {
            if (Inside(node) || Temporary(node.SchemaObjectName) || node.Definition is null) return;
            Add(Table(node, node.SchemaObjectName, node.Definition));
        }

        public override void Visit(AlterTableAddTableElementStatement node)
        {
            if (Inside(node) || Temporary(node.SchemaObjectName)) return;
            Add(new SqlAlterTable { Adds = Table(node, node.SchemaObjectName, node.Definition) });
        }

        public override void Visit(AlterTableAlterColumnStatement node)
        {
            if (Inside(node) || Temporary(node.SchemaObjectName) || node.DataType is null) return;
            var alter = new SqlAlterTable { Adds = Located(new SqlTable(), node, Name(node.SchemaObjectName)) };
            alter.Alters.Add(new SqlColumn { Name = node.ColumnIdentifier.Value, Type = DbText.TypeName(Text(node.DataType)), Nullable = node.AlterTableAlterColumnOption != AlterTableAlterColumnOption.NotNull });
            Add(alter);
        }

        public override void Visit(AlterTableDropTableElementStatement node)
        {
            if (Inside(node) || Temporary(node.SchemaObjectName)) return;
            var alter = new SqlAlterTable { Adds = Located(new SqlTable(), node, Name(node.SchemaObjectName)) };
            foreach (var e in node.AlterTableDropTableElements.Where(e => e.Name is not null))
                (e.TableElementType == TableElementType.Column ? alter.DropColumns : alter.DropConstraints).Add(e.Name.Value);
            Add(alter);
        }

        public override void Visit(DropObjectsStatement node)
        {
            if (Inside(node)) return;
            SqlKind? kind = node switch
            {
                DropTableStatement => SqlKind.Table, DropViewStatement => SqlKind.View, DropProcedureStatement or DropFunctionStatement => SqlKind.Routine,
                DropTriggerStatement => SqlKind.Trigger, _ => null,
            };
            if (kind is null) return;
            foreach (var o in node.Objects.Where(o => !Temporary(o))) Add(new SqlDrop(kind.Value, Name(o)));
        }

        public override void Visit(ProcedureStatementBody node)
        {
            if (Inside(node) || node.ProcedureReference?.Name is not { } name || Temporary(name)) return;
            Add(Routine(node, name, node.Parameters, false));
        }

        public override void Visit(FunctionStatementBody node)
        {
            if (Inside(node) || node.Name is null) return;
            Add(Routine(node, node.Name, node.Parameters, true));
        }

        public override void Visit(ViewStatementBody node)
        {
            if (Inside(node)) return;
            Add(Body(Located(new SqlView(), node, Name(node.SchemaObjectName)), node));
        }

        public override void Visit(TriggerStatementBody node)
        {
            if (Inside(node)) return;
            _moduleEnd = node.LastTokenIndex;
            // DDL triggers (ON DATABASE, ON ALL SERVER) have no table and get no entry.
            if (node.TriggerObject?.Name is not { } table) return;
            var on = Name(table);
            var name = Name(node.Name);
            // A trigger lives in its table's schema.
            if (node.Name.SchemaIdentifier is null) name = name with { Schema = on.Schema };
            var trigger = Located(new SqlTrigger { Table = on, Timing = node.TriggerType == TriggerType.InsteadOf ? "instead of" : "after" }, node, name);
            trigger.Events.AddRange(node.TriggerActions.Select(a => a.TriggerActionType.ToString().ToLowerInvariant()));
            Add(Body(trigger, node));
        }

        public override void Visit(ExecuteSpecification node)
        {
            if (!Inside(node) && Dynamic(node)) script.DynamicSql++;
        }

        SqlRoutine Routine(TSqlStatement node, SchemaObjectName name, IList<ProcedureParameter> parameters, bool function)
        {
            var routine = Located(new SqlRoutine { IsFunction = function }, node, Name(name));
            foreach (var p in parameters)
                routine.Parameters.Add(new(p.VariableName.Value, p.DataType is null ? "" : DbText.TypeName(Text(p.DataType)), p.Modifier == ParameterModifier.Output));
            return Body(routine, node);
        }

        T Body<T>(T module, TSqlFragment node) where T : SqlModule
        {
            _moduleEnd = node.LastTokenIndex;
            var body = new BodyVisitor();
            node.Accept(body);
            module.Reads = body.Result(body.Reads);
            module.Writes = body.Result(body.Writes);
            module.Calls = body.Result(body.Calls);
            module.DynamicSql = body.DynamicSql;
            return module;
        }

        static SqlTable Table(TSqlFragment node, SchemaObjectName name, TableDefinition definition)
        {
            var table = Located(new SqlTable(), node, Name(name));
            void ForeignKey(ForeignKeyConstraintDefinition fk, IEnumerable<string> columns)
            {
                if (fk.ReferenceTableName is null) return;
                table.ForeignKeys.Add(new SqlForeignKey { Name = fk.ConstraintIdentifier?.Value, Columns = columns.ToList(), References = Name(fk.ReferenceTableName), Line = fk.StartLine });
            }
            foreach (var c in definition.ColumnDefinitions)
            {
                var primary = c.Constraints.OfType<UniqueConstraintDefinition>().FirstOrDefault(u => u.IsPrimaryKey);
                if (primary is not null) table.PrimaryKeyName = primary.ConstraintIdentifier?.Value;
                table.Columns.Add(new SqlColumn
                {
                    Name = c.ColumnIdentifier.Value,
                    Type = c.DataType is null ? "computed" : DbText.TypeName(Text(c.DataType)),
                    PrimaryKey = primary is not null,
                    Nullable = primary is null && (c.Constraints.OfType<NullableConstraintDefinition>().FirstOrDefault()?.Nullable ?? c.IdentityOptions is null),
                });
                foreach (var fk in c.Constraints.OfType<ForeignKeyConstraintDefinition>()) ForeignKey(fk, [c.ColumnIdentifier.Value]);
            }
            foreach (var constraint in definition.TableConstraints)
            {
                if (constraint is ForeignKeyConstraintDefinition fk) ForeignKey(fk, fk.Columns.Select(i => i.Value));
                if (constraint is not UniqueConstraintDefinition { IsPrimaryKey: true } pk) continue;
                table.PrimaryKeyName = pk.ConstraintIdentifier?.Value;
                foreach (var key in pk.Columns.Select(k => k.Column.MultiPartIdentifier.Identifiers[^1].Value))
                {
                    table.PrimaryKeyColumns.Add(key);
                    if (table.Columns.FirstOrDefault(c => c.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } column) { column.PrimaryKey = true; column.Nullable = false; }
                }
            }
            return table;
        }
    }

    static bool Dynamic(ExecuteSpecification node) => node.ExecutableEntity switch
    {
        ExecutableStringList => true,
        ExecutableProcedureReference p => p.ProcedureReference?.ProcedureVariable is not null
            || "sp_executesql".Equals(p.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>Collects what one routine, trigger or view body touches.</summary>
    sealed class BodyVisitor : TSqlFragmentVisitor
    {
        // The bool says the name was written with a schema; a bare name can be a CTE.
        public readonly List<(SqlName Name, int Line, bool Qualified)> Reads = [], Writes = [], Calls = [];
        public int DynamicSql;
        readonly HashSet<string> _ctes = new(StringComparer.OrdinalIgnoreCase) { "inserted", "deleted" };
        readonly HashSet<TSqlFragment> _written = [], _skipped = [];

        void Add(List<(SqlName, int, bool)> list, SchemaObjectName n)
        {
            // Four-part names go through a linked server; those are not read in v1.
            if (Temporary(n) || n.ServerIdentifier is not null) return;
            if (n.SchemaIdentifier?.Value is { } s && (s.Equals("sys", StringComparison.OrdinalIgnoreCase) || s.Equals("INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase))) return;
            list.Add((Name(n), n.StartLine, n.SchemaIdentifier is not null));
        }

        // ponytail: a CTE name hides a same-named dbo table for the whole body, not just its own statement. Scope it per statement if that ever bites.
        public List<SqlRef> Result(List<(SqlName Name, int Line, bool Qualified)> list) => list
            .Where(r => r.Qualified || !_ctes.Contains(r.Name.Name))
            .GroupBy(r => (r.Name.Database?.ToLowerInvariant(), r.Name.Key)).Select(g => new SqlRef(g.First().Name, g.First().Line)).ToList();

        public override void Visit(CommonTableExpression node) => _ctes.Add(node.ExpressionName.Value);

        public override void Visit(DataModificationSpecification node)
        {
            if (node.Target is not NamedTableReference named) return;
            var target = named;
            // UPDATE o SET … FROM dbo.Orders o: the target names an alias, and the table written is the one in FROM.
            var from = (node as UpdateSpecification)?.FromClause ?? (node as DeleteSpecification)?.FromClause;
            if (from is not null && named.SchemaObject is { SchemaIdentifier: null, BaseIdentifier.Value: var alias })
            {
                var tables = new TableCollector();
                from.Accept(tables);
                var real = tables.Found.FirstOrDefault(t => (t.Alias?.Value ?? (t as NamedTableReference)?.SchemaObject.BaseIdentifier?.Value ?? "").Equals(alias, StringComparison.OrdinalIgnoreCase));
                if (real is not null) _skipped.Add(named);
                // The alias can also stand for a table variable, a function or a subquery: nothing to record then.
                if (real is not null) target = real as NamedTableReference;
            }
            if (target is not null) _written.Add(target);
        }

        public override void Visit(NamedTableReference node)
        {
            if (!_skipped.Contains(node)) Add(_written.Contains(node) ? Writes : Reads, node.SchemaObject);
        }

        public override void Visit(SelectStatement node)
        {
            if (node.Into is not null) Add(Writes, node.Into);
        }

        public override void Visit(TruncateTableStatement node) => Add(Writes, node.TableName);

        public override void Visit(ExecuteSpecification node)
        {
            if (Dynamic(node)) DynamicSql++;
            else if (node.ExecutableEntity is ExecutableProcedureReference { ProcedureReference.ProcedureReference.Name: { } name }) Add(Calls, name);
        }

        public override void Visit(SchemaObjectFunctionTableReference node)
        {
            if (node.SchemaObject.SchemaIdentifier is not null && !Methods.Contains(node.SchemaObject.BaseIdentifier.Value, StringComparer.OrdinalIgnoreCase)) Add(Calls, node.SchemaObject);
        }

        public override void Visit(FunctionCall node)
        {
            if (node.CallTarget is not MultiPartIdentifierCallTarget { MultiPartIdentifier.Identifiers: { Count: 1 or 2 } ids }) return;
            if (Methods.Contains(node.FunctionName.Value, StringComparer.OrdinalIgnoreCase) || node.FunctionName.Value.StartsWith("ST", StringComparison.Ordinal)) return;
            var schema = ids[^1].Value;
            if (schema.Length == 0 || schema.Equals("sys", StringComparison.OrdinalIgnoreCase)) return;
            Calls.Add((new(ids.Count == 2 ? ids[0].Value : null, schema, node.FunctionName.Value), node.StartLine, true));
        }
    }

    sealed class TableCollector : TSqlFragmentVisitor
    {
        public readonly List<TableReferenceWithAlias> Found = [];
        public override void Visit(TableReferenceWithAlias node) => Found.Add(node);
    }
}
