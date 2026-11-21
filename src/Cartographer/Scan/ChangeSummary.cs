using System.Text;

namespace Cartographer;

/// <summary>What one repository's scan changed in the graph: the entries on disk compared with the atlas's last commit.</summary>
public static class ChangeSummary
{
    /// <param name="entries">The entries this scan touched; IsNew spares looking up an old version that cannot exist.</param>
    public static string Build(Atlas atlas, string folder, string commit7, IEnumerable<(string Id, bool IsNew)> entries)
    {
        List<string> added = [], removed = [], retired = [], linksAdded = [], linksRemoved = [], asked = [], closed = [], drift = [];
        foreach (var (id, isNew) in entries.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            // ponytail: one "git show" per changed entry; switch to "git cat-file --batch" if rescans of huge repositories get slow.
            var old = isNew ? null : Old(atlas.Root, id);
            var now = atlas.Find(id);
            if (old is null && now is null) continue;
            var wasLive = old is { Status: null };
            if (now is null) removed.Add($"{id} ({old!.Kind})");
            else if (now.Status == "retired") { if (wasLive) retired.Add($"{id} ({now.Kind})"); }
            else if (old is null) added.Add($"{id} ({now.Kind})");

            static string Show(string id, Link l) => $"{id}: {l.Type} {l.To ?? l.Name + " (unresolved)"} ({l.Basis})";
            var oldLinks = old?.Links ?? [];
            var nowLinks = now?.Links ?? [];
            linksAdded.AddRange(nowLinks.Where(l => oldLinks.All(o => o.Identity != l.Identity)).Select(l => Show(id, l)));
            linksRemoved.AddRange(oldLinks.Where(l => nowLinks.All(n => n.Identity != l.Identity)).Select(l => Show(id, l)));

            static List<Question> Waiting(Entry? e) => e is null ? [] : QuestionList.Parse(e.Body).Items.Where(q => q.State is "open" or "needs-person").ToList();
            var oldQuestions = Waiting(old);
            var nowQuestions = Waiting(now);
            asked.AddRange(nowQuestions.Where(q => oldQuestions.All(o => o.Key != q.Key)).Select(q => $"{id}: {q.Missing}"));
            closed.AddRange(oldQuestions.Where(q => nowQuestions.All(n => n.Key != q.Key)).Select(q => $"{id}: {q.Missing}"));

            drift.AddRange((now?.Drift ?? []).Where(d => (old?.Drift ?? []).All(o => o.What != d.What)).Select(d => $"{id}: {d.What}"));
        }

        var sb = new StringBuilder($"# Scan {folder} at {commit7}\n\n");
        sb.Append($"Parts: {added.Count} added, {removed.Count} removed, {retired.Count} retired. Links: {linksAdded.Count} added, {linksRemoved.Count} removed. ");
        sb.Append($"Open questions: {asked.Count} new, {closed.Count} closed. Drift: {drift.Count} new.\n");
        void Section(string title, List<string> lines)
        {
            if (lines.Count > 0) sb.Append($"\n## {title}\n\n{string.Join('\n', lines.Select(l => "- " + l))}\n");
        }
        Section("Parts added", added);
        Section("Parts removed", removed);
        Section("Parts retired", retired);
        Section("Links added", linksAdded);
        Section("Links removed", linksRemoved);
        Section("New open questions", asked);
        Section("Closed open questions", closed);
        Section("New drift", drift);
        return sb.ToString();
    }

    /// <summary>The entry as the atlas's last commit has it, or null when it was not there (or could not be read).</summary>
    static Entry? Old(string root, string id)
    {
        if (Checkout.Git(root, "show", $"HEAD:{id}.md") is not { } text) return null;
        try { return EntryFile.Parse(id, text); }
        catch (EntryFormatException) { return null; }
    }
}
