using System.Text;

namespace Cartographer;

/// <summary>Writes everything under _generated/ (spec section 4). Output depends only on the entries.</summary>
public static class Indexer
{
    public const string Folder = "_generated";

    /// <summary>For each entry, the links that point at it: (link type, from ID, basis).</summary>
    public static Dictionary<string, List<(string Type, string From, string Basis)>> Inbound(Atlas atlas)
    {
        var inbound = new Dictionary<string, List<(string, string, string)>>();
        void Add(string to, string type, string from, string basis)
        {
            if (!inbound.TryGetValue(to, out var list)) inbound[to] = list = [];
            list.Add((type, from, basis));
        }
        foreach (var e in atlas.Entries.Values)
        {
            foreach (var l in e.Links ?? []) if (l.To is not null) Add(l.To, l.Type, e.Id, l.Basis);
            foreach (var s in e.Systems ?? []) Add(s.Id, "member", e.Id, s.Basis);
        }
        return inbound;
    }

    public static void Write(Atlas atlas)
    {
        var files = Build(atlas);
        var root = Path.Combine(atlas.Root, Folder);
        if (Directory.Exists(root)) Directory.Delete(root, true);
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
    }

    /// <summary>Relative path under _generated/ to file text.</summary>
    public static SortedDictionary<string, string> Build(Atlas atlas)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var cap = atlas.Config.Limits.IndexRows;
        var entries = atlas.Entries.Values.ToList();
        static string Dir(string id) => id[..id.LastIndexOf('/')];

        // Folder indexes.
        var dirs = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var e in entries)
            for (var d = Dir(e.Id); ; d = Dir(d)) { dirs.Add(d); if (!d.Contains('/')) break; }
        foreach (var dir in dirs)
        {
            var sb = new StringBuilder($"# {dir}\n");
            var children = dirs.Where(d => d.StartsWith(dir + "/") && !d[(dir.Length + 1)..].Contains('/')).ToList();
            if (children.Count > 0)
            {
                sb.Append("\n## Folders\n\n| Folder | Entries |\n|---|---|\n");
                foreach (var c in children) sb.Append($"| [{c}/]({c[(dir.Length + 1)..]}/index.md) | {entries.Count(e => e.Id.StartsWith(c + "/"))} |\n");
            }
            var rows = entries.Where(e => Dir(e.Id) == dir).ToList();
            if (rows.Count > cap)
            {
                sb.Append("\n## Entries\n\n| Part | From | To | Entries |\n|---|---|---|---|\n");
                var n = 0;
                foreach (var chunk in rows.Chunk(cap))
                {
                    n++;
                    sb.Append($"| [index-{n}.md](index-{n}.md) | {chunk[0].Id} | {chunk[^1].Id} | {chunk.Length} |\n");
                    files[$"{dir}/index-{n}.md"] = $"# {dir} (part {n})\n\n" + Table(chunk);
                }
            }
            else if (rows.Count > 0) sb.Append("\n## Entries\n\n" + Table(rows));
            files[$"{dir}/index.md"] = sb.ToString();
        }

        // Root index.
        var rootIndex = new StringBuilder("# Atlas index\n\nStart here. Open one folder index, then one entry. An entry's ID is its path without `.md`.\n\n| Folder | Entries |\n|---|---|\n");
        foreach (var top in Atlas.Folders.Where(dirs.Contains)) rootIndex.Append($"| [{top}/]({top}/index.md) | {entries.Count(e => e.Id.StartsWith(top + "/"))} |\n");
        rootIndex.Append("\nAlso: [open questions](open-questions.md), [provided names](provided-names.md). For an entry `<id>`, what uses it is in `<id>.used-by.md` here.\n");
        files["index.md"] = rootIndex.ToString();

        // Reverse lists.
        foreach (var (to, list) in Inbound(atlas))
        {
            var sb = new StringBuilder($"# Used by: {to}\n\nDirect users only. For indirect reach run `cartographer impact {to}`.\n");
            foreach (var group in list.GroupBy(l => l.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.Append($"\n## {Reverse(group.Key)}\n\n");
                foreach (var l in group.OrderBy(l => l.From, StringComparer.Ordinal).ThenBy(l => l.Basis, StringComparer.Ordinal).Distinct()) sb.Append($"- {l.From} ({l.Basis})\n");
            }
            files[$"{to}.used-by.md"] = sb.ToString();
        }

        // Open questions.
        var questions = entries.SelectMany(e => QuestionList.Parse(e.Body).Items.Select(q => (Entry: e, Q: q))).ToList();
        var qs = new StringBuilder("# Open questions\n");
        foreach (var state in new[] { "needs-person", "answered", "open" })
        {
            var inState = questions.Where(x => x.Q.State == state).ToList();
            if (inState.Count == 0) continue;
            qs.Append($"\n## {state} ({inState.Count})\n\n| Entry | Question | Missing | Waiting on | Attempts |\n|---|---|---|---|---|\n");
            foreach (var (e, q) in inState) qs.Append($"| {e.Id} | {q.Id} | {Cell(q.Missing)} | {Cell(q.WaitingOn)} | {q.Attempts} |\n");
        }
        if (questions.Count == 0) qs.Append("\nNone.\n");
        files["open-questions.md"] = qs.ToString();

        // Provided names.
        var names = new StringBuilder("# Provided names\n\n| Name | As | Component |\n|---|---|---|\n");
        foreach (var (name, kind, id) in entries.SelectMany(e => (e.Provides ?? []).Select(p => (p.Name, p.As, e.Id))).Distinct()
                     .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.As, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal))
            names.Append($"| {Cell(name)} | {kind} | {id} |\n");
        files["provided-names.md"] = names.ToString();
        return files;
    }

    static string Table(IEnumerable<Entry> rows)
    {
        var sb = new StringBuilder("| ID | Kind | Name | Summary |\n|---|---|---|---|\n");
        foreach (var e in rows) sb.Append($"| {e.Id} | {e.Kind}{(e.Type is null ? "" : $" ({e.Type})")}{(e.Status is null ? "" : $", {e.Status}")} | {Cell(e.Name)} | {Cell(e.Summary)} |\n");
        return sb.ToString();
    }

    public static string Reverse(string linkType) => linkType switch
    {
        "depends-on" => "Depended on by",
        "calls" => "Called by",
        "reads" => "Read by",
        "writes" => "Written by",
        "uses" => "Used by",
        "publishes-to" => "Published to by",
        "consumes-from" => "Consumed by",
        "sends-to" => "Sent to by",
        "receives-from" => "Received from by",
        "links-to" => "Linked from",
        "references" => "Referenced by",
        "fires-on" => "Triggers",
        "defines" => "Defined by",
        "member" => "Members",
        _ => linkType,
    };

    static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ");
}
