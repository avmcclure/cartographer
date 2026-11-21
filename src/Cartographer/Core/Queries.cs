using System.Text;

namespace Cartographer;

/// <summary>Read-only answers kept small, so an agent spends little context on them.</summary>
public static class Queries
{
    public static string Find(Atlas atlas, string text, int max = 30)
    {
        bool Has(string? s) => s?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
        var hits = atlas.Entries.Values
            .Select(e => (e, rank: Has(e.Name) || Has(e.Id) ? 0 : (e.Provides ?? []).Any(p => Has(p.Name)) || (e.Aliases ?? []).Any(Has) ? 1 : Has(e.Summary) ? 2 : -1))
            .Where(x => x.rank >= 0).OrderBy(x => x.rank).ThenBy(x => x.e.Id, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        foreach (var (e, _) in hits.Take(max)) sb.Append($"{e.Id}\t{e.Kind}\t{e.Summary}\n");
        if (hits.Count > max) sb.Append($"… {hits.Count - max} more; narrow the search\n");
        return hits.Count == 0 ? $"nothing matches '{text}'\n" : sb.ToString();
    }

    public static string Show(Atlas atlas, string id)
    {
        var e = Require(atlas, id);
        var sb = new StringBuilder($"{e.Id}\n{e.Kind}{(e.Type is null ? "" : $" ({e.Type})")}: {e.Name}{(e.Status is null ? "" : $" [{e.Status}]")}\n{e.Summary}\n");
        foreach (var group in (e.Links ?? []).GroupBy(l => l.Type))
            sb.Append($"{group.Key}: {string.Join(", ", group.Select(l => (l.To ?? $"{l.Name} (unresolved)") + (l.Basis == Basis.Extracted ? "" : $" [{l.Basis}]")))}\n");
        var overview = Sections.Get(e.Body, Sections.Overview);
        if (overview.Length > 0) sb.Append('\n' + overview + '\n');
        var questions = QuestionList.Parse(e.Body).Items.Count(q => q.State is "open" or "needs-person");
        if (questions > 0) sb.Append($"\n{questions} open question(s); see the entry.\n");
        return sb.ToString();
    }

    public static string UsedBy(Atlas atlas, string id)
    {
        Require(atlas, id);
        var inbound = Indexer.Inbound(atlas).GetValueOrDefault(id) ?? [];
        if (inbound.Count == 0) return $"nothing in the atlas links to {id}\n";
        var sb = new StringBuilder();
        foreach (var group in inbound.GroupBy(l => l.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append($"{Indexer.Reverse(group.Key)}:\n");
            foreach (var l in group.OrderBy(l => l.From, StringComparer.Ordinal)) sb.Append($"  {l.From} ({l.Basis})\n");
        }
        return sb.ToString();
    }

    /// <summary>What could be affected by changing an entry: everything that links to it, followed outwards. A repository or database starts from everything inside it.</summary>
    public static string Impact(Atlas atlas, string id, int depth = 3)
    {
        Require(atlas, id);
        var inbound = Indexer.Inbound(atlas);
        var area = id.EndsWith("/repository") || id.EndsWith("/database") ? Ids.Area(id) : null;
        var start = area is null ? [id] : atlas.Entries.Keys.Where(k => k.StartsWith(area + "/")).ToList();
        var seen = new HashSet<string>(start);
        var frontier = start;
        var sb = new StringBuilder();
        for (var level = 1; level <= depth && frontier.Count > 0; level++)
        {
            var next = new List<string>();
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var target in frontier)
                foreach (var l in inbound.GetValueOrDefault(target) ?? [])
                {
                    if (l.Type == "member" || (area is not null && l.From.StartsWith(area + "/"))) continue;
                    lines.Add($"  {l.From} {l.Type} {target} ({l.Basis})");
                    if (seen.Add(l.From)) next.Add(l.From);
                }
            if (lines.Count > 0) sb.Append($"{(level == 1 ? "Directly" : $"At {level} steps")}:\n{string.Join('\n', lines)}\n");
            frontier = next;
        }
        var affected = seen.Count - start.Count;
        return affected == 0 ? $"nothing in the atlas links to {id}\n" : $"{affected} entr{(affected == 1 ? "y" : "ies")} could be affected by a change to {id}\n" + sb;
    }

    static Entry Require(Atlas atlas, string id) => atlas.Find(id) ?? throw new UsageException($"no entry '{id}'. Try: cartographer find <part of its name>");
}

/// <summary>A mistake in how a command was called or set up; shown without a stack trace, exit code 2.</summary>
public sealed class UsageException(string message) : Exception(message);
