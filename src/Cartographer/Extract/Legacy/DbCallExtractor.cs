using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>
/// Order 60: database calls written in code. Literal stored procedure names, literal SQL with plain table names and
/// literal connection strings become unresolved links on the component the file belongs to. Nothing built at run time
/// is guessed at: it is counted for an agent. No credential value is ever recorded.
/// </summary>
public sealed class DbCallExtractor : IExtractor
{
    const int Nearby = 10;   // lines between a command's text and its CommandType
    const string Ident = @"(?:\[[^\]]+\]|[A-Za-z_]\w*)(?:\.(?:\[[^\]]+\]|[A-Za-z_]\w*)){0,2}";
    const RegexOptions I = RegexOptions.IgnoreCase;

    static readonly Regex StoredProcType = new(@"(?:\b([A-Za-z_][\w.]*))?\.?\bCommandType\s*=\s*(?:adCmdStoredProc\b|4\b|(?:System\.Data\.)?CommandType\.StoredProcedure\b)", I);
    static readonly Regex CommandText = new(@"(?:\b([A-Za-z_][\w.]*))?\.?\bCommandText\s*=\s*@?""([^""\r\n]*)""(?![ \t]*[&+""])", I);
    static readonly Regex NewCommand = new(@"(?:\b(\w+)\s*(?:=\s*new|As\s+New)\s+|\bnew\s+)(?:[\w.]+\.)?(?:Sql|OleDb|Odbc)Command\s*\(\s*@?""([^""\r\n]*)""(?![ \t]*[&+""])", I);
    static readonly Regex ProcedureName = new($"^{Ident}$");
    static readonly Regex Exec = new($@"^\s*EXEC(?:UTE)?\b(?:\s+(?:@\w+\s*=\s*)?({Ident})(?=[\s;(]|$))?", I);
    static readonly Regex SqlStart = new(@"^\s*(SELECT|INSERT|UPDATE|DELETE)\s", I);
    static readonly Regex SqlShape = new(@"^\s*(SELECT\s|INSERT\s+INTO\s|UPDATE\s+\S|DELETE\s+FROM\s)", I);
    static readonly Regex Insert = new($@"^\s*INSERT\s+(?:INTO\s+)?({Ident})\s*(?:\(|VALUES\b|SELECT\b|DEFAULT\b|EXEC)", I);
    static readonly Regex Update = new($@"^\s*UPDATE\s+({Ident})\s+SET\b", I);
    static readonly Regex Delete = new($@"^\s*DELETE\s+(?:FROM\s+)?({Ident})(?=[\s;]|$)", I);
    static readonly Regex From = new(@"\bFROM\s+(.*?)(?=\b(?:WHERE|GROUP|ORDER|HAVING|UNION)\b|[();]|$)", I);
    static readonly Regex Join = new(@",|\b(?:(?:INNER|LEFT|RIGHT|FULL|CROSS)\s+)?(?:OUTER\s+)?JOIN\b", I);
    static readonly Regex TableAndAlias = new($@"^\s*({Ident})(?=\s|$)(?:\s+(?:AS\s+)?([A-Za-z_]\w*))?", I);
    static readonly Regex DatabaseKey = new(@"(?:^|;)\s*(?:Initial Catalog|Database)\s*=\s*([^;]*)(;?)", I);
    static readonly Regex ServerKey = new(@"(?:^|;)\s*(?:Data Source|Server|Address|Addr|Network Address)\s*=\s*([^;]*)(;?)", I);
    static readonly Regex CredentialKey = new(@"(?:^|;)\s*(?:Password|Pwd)\s*=", I);
    static readonly Regex IntegratedKey = new(@"(?:^|;)\s*(?:Integrated Security|Trusted_Connection)\s*=", I);
    // Only names made of these characters are ever written down, so a credential cannot ride along in one.
    static readonly Regex SafeDatabase = new(@"^[\w.\-$ ]+$");
    static readonly Regex SafeServer = new(@"^[\w.\-$\\,:() ]+$");

    public int Order => 60;

    public void Extract(Extraction x)
    {
        foreach (var group in x.Repo.WithExtension(".asp", ".asa", ".inc", ".cls", ".bas", ".frm", ".cs", ".vb").GroupBy(f => x.ComponentAt(f)).Where(g => g.Key is not null))
        {
            var c = group.Key!;
            var connections = new List<string>();
            var runTime = new List<string>();
            foreach (var file in group)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                var csharp = extension == ".cs";
                var text = LegacyText.Read(x.Repo, file);
                var found = new List<(int Line, string Type, string Name)>();

                // A command whose text is a bare literal name and whose type is "stored procedure" within a few lines.
                bool Commented(Match m) => text[(text.LastIndexOf('\n', m.Index) + 1)..m.Index].TrimStart() is var before && (csharp ? before.StartsWith("//") : before.StartsWith('\''));
                var types = StoredProcType.Matches(text).Where(m => !Commented(m)).Select(m => (Line: LegacyText.Line(text, m.Index), Object: m.Groups[1].Value.TrimEnd('.'))).ToList();
                foreach (var (m, created) in CommandText.Matches(text).Select(m => (m, false)).Concat(NewCommand.Matches(text).Select(m => (m, true))))
                {
                    var line = LegacyText.Line(text, m.Index);
                    var command = m.Groups[1].Value.TrimEnd('.');
                    if (Commented(m) || !ProcedureName.IsMatch(m.Groups[2].Value)) continue;
                    if (types.Any(t => Math.Abs(t.Line - line) <= Nearby && (t.Object.Equals(command, StringComparison.OrdinalIgnoreCase) || created && t.Object == "" && t.Line >= line)))
                        found.Add((line, "calls", Name(m.Groups[2].Value)));
                }

                var literals = Literals(text, csharp, asp: extension is ".asp" or ".asa" or ".inc");
                foreach (var literal in literals)
                {
                    var joined = literal.JoinedBefore || literal.JoinedAfter || literal.Interpolated;
                    if (Exec.Match(literal.Value) is { Success: true } exec)
                    {
                        var name = exec.Groups[1];
                        var cutShort = name.Success && name.Index + name.Length == literal.Value.Length && (literal.JoinedAfter || literal.Interpolated);
                        if (name.Success && !cutShort && !Name(name.Value).EndsWith("sp_executesql", StringComparison.OrdinalIgnoreCase)) found.Add((literal.Line, "calls", Name(name.Value)));
                        else if (joined || name.Success) runTime.Add(file);
                    }
                    else if (SqlStart.IsMatch(literal.Value))
                    {
                        if (!joined) found.AddRange(Tables(literal.Value).Select(t => (literal.Line, t.Type, t.Name)));
                        else if (SqlShape.IsMatch(literal.Value)) runTime.Add(file);
                    }
                    else if (DatabaseKey.Match(literal.Value) is { Success: true } database)
                    {
                        // A value counts only when it is whole: closed by ';' or by the end of a string nothing is appended to.
                        string? Whole(Match m, Regex safe) => m.Success && m.Groups[1].Value.Trim() is var v && safe.IsMatch(v)
                            && (m.Groups[2].Length > 0 || m.Index + m.Length == literal.Value.Length && !literal.JoinedAfter && !literal.Interpolated) ? v : null;
                        if (Whole(database, SafeDatabase) is not { } name) continue;
                        var server = Whole(ServerKey.Match(literal.Value), SafeServer);
                        var around = literals.Where(l => l.Line == literal.Line).ToList();   // the other pieces of a string joined on one line
                        var signIn = around.Any(l => CredentialKey.IsMatch(l.Value)) ? "a credential is in the code (not recorded here)"
                            : around.Any(l => IntegratedKey.IsMatch(l.Value)) ? "integrated sign-in, no credential" : "no credential";
                        var words = $"server {(server is null ? "not a literal" : $"`{server}`")}, database `{name}`; {signIn}";
                        if (!connections.Any(k => k.EndsWith(words))) connections.Add($"- Connection string in `{file}` line {literal.Line}: {words}");
                        found.Add((literal.Line, "uses", name));
                    }
                }
                foreach (var (line, type, name) in found.OrderBy(f => f.Line)) LegacyText.Link(x, c, type, name, "sql", file, line);
            }

            var block = new StringBuilder();
            foreach (var connection in connections) block.Append(connection + ".\n");
            if (runTime.Count > 0)
            {
                var files = runTime.GroupBy(f => f).Select(g => $"`{g.Key}` ({g.Count()})").ToList();
                block.Append($"- SQL built at run time, not linked: {LegacyText.Plural(runTime.Count, "statement", "statements")} in {string.Join(", ", files.Take(5))}{(files.Count > 5 ? $" and {files.Count - 5} more files" : "")}.\n");
            }
            if (block.Length > 0) LegacyText.Append(c, block.ToString());
        }
    }

    static string Name(string written) => written.Replace("[", "").Replace("]", "");

    /// <summary>
    /// The tables a literal statement reads and writes.
    /// ponytail: regular expressions, not a SQL parser. Derived tables and CTEs give only their inner tables, and
    /// user-interface text shaped like SQL ("Select a customer from the list") gives a link that never resolves.
    /// Use ScriptDom here if those show up as noise.
    /// </summary>
    internal static List<(string Type, string Name)> Tables(string sql)
    {
        var found = new List<(string Type, string Name)>();
        var s = Regex.Replace(sql, @"\s+", " ").Trim();
        var write = new[] { Insert, Update, Delete }.Select(r => r.Match(s)).FirstOrDefault(m => m.Success);
        if (Delete.Match(s) is { Success: true } delete) s = s[delete.Length..];   // DELETE FROM x: x is written, not read
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match from in From.Matches(s))
            foreach (var piece in Join.Split(from.Groups[1].Value))
            {
                if (TableAndAlias.Match(piece) is not { Success: true } table) continue;
                found.Add(("reads", Name(table.Groups[1].Value)));
                if (table.Groups[2].Success) aliases[table.Groups[2].Value] = Name(table.Groups[1].Value);
            }
        if (write is not null)
        {
            var target = Name(write.Groups[1].Value);
            found.Add(("writes", aliases.GetValueOrDefault(target, target)));   // UPDATE o SET … FROM Orders o
        }
        return found.Distinct().ToList();
    }

    internal sealed record Literal(int Line, string Value, bool JoinedBefore, bool JoinedAfter, bool Interpolated);

    /// <summary>
    /// Every string literal outside comments, with whether it is joined to something by &amp; or +.
    /// VB family: "…" with "" for a quote, ' starts a comment. C#: regular, verbatim, interpolated and raw strings.
    /// </summary>
    internal static List<Literal> Literals(string text, bool csharp, bool asp = false)
    {
        var found = new List<Literal>();
        var line = 1;
        char At(int k) => k >= 0 && k < text.Length ? text[k] : '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\n') { line++; continue; }
            if (csharp ? ch == '/' && At(i + 1) == '/' : ch == '\'')
            {
                var end = text.IndexOf('\n', i) is >= 0 and var newline ? newline : text.Length;
                if (asp && text.IndexOf("%>", i, end - i, StringComparison.Ordinal) is >= 0 and var close) end = close;   // a comment ends with its script block
                i = end - 1;
                continue;
            }
            if (csharp && ch == '/' && At(i + 1) == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) break;
                line += text.AsSpan(i, end - i).Count('\n');
                i = end + 1;
                continue;
            }
            if (csharp && ch == '\'')
            {
                var end = text.IndexOf('\'', Math.Min(text.Length, i + (At(i + 1) == '\\' ? 3 : 2)));
                if (end < 0) break;
                i = end;
                continue;
            }
            if (ch != '"') continue;

            var (start, startLine) = (i, line);
            var prefix = At(i - 1) is '@' or '$' ? At(i - 2) is '@' or '$' ? 2 : 1 : 0;
            var marks = text[(i - prefix)..i];
            var verbatim = csharp && marks.Contains('@');
            var value = new StringBuilder();
            int j;
            if (csharp && text.AsSpan(i).StartsWith("\"\"\""))
            {
                j = text.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
                if (j < 0) break;
                value.Append(text, i + 3, j - i - 3);
                line += text.AsSpan(i, j - i).Count('\n');
                j += 2;
            }
            else
            {
                var closed = false;
                for (j = i + 1; j < text.Length && !closed; j++)
                {
                    var c = text[j];
                    if (c == '"' && (verbatim || !csharp) && At(j + 1) == '"') { value.Append('"'); j++; }
                    else if (c == '"') closed = true;
                    else if (c == '\\' && csharp && !verbatim) { value.Append(At(j + 1) is 'n' or 'r' or 't' ? ' ' : At(j + 1)); j++; }
                    else if (c == '\n' && !verbatim) break;   // not a string after all
                    else { if (c == '\n') line++; value.Append(c); }
                }
                if (!closed) { i = j - 1; continue; }
                j--;   // the closing quote
            }

            var before = start - prefix - 1;
            while (char.IsWhiteSpace(At(before))) before--;
            if (!csharp && At(before) == '_') { before--; while (char.IsWhiteSpace(At(before))) before--; }   // VB line continuation
            var after = j + 1;
            while (At(after) is ' ' or '\t' || csharp && At(after) is '\r' or '\n') after++;
            found.Add(new(startLine, value.ToString(),
                At(before) is '&' or '+' || At(before) == '=' && At(before - 1) is '&' or '+',
                At(after) is '&' or '+',
                marks.Contains('$') && value.ToString().Contains('{')));
            i = j;
        }
        return found;
    }
}
