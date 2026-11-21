namespace Cartographer;

public sealed record RetryItem(string Entry, string Question, string Reason, bool Person);

/// <summary>Open questions worth trying again because what they wait on has changed (spec section 7, "question retry").</summary>
public static class QuestionRetry
{
    /// <summary>People's questions first, then by entry and question.</summary>
    public static List<RetryItem> Find(Atlas atlas)
    {
        var providers = atlas.OfKind(Kinds.Component).Where(c => c.Status is null)
            .SelectMany(c => (c.Provides ?? []).Select(p => (p.Name, c.Id)))
            .ToLookup(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
        // ponytail: two git calls per waiting question, cached per path; batch through one "git log --name-only" if an atlas has thousands.
        var lastCommit = new Dictionary<string, string?>();
        var newer = new Dictionary<(string, string), bool>();
        bool ChangedSince(string asking, string target)
        {
            if (!lastCommit.TryGetValue(asking, out var commit))
                lastCommit[asking] = commit = Checkout.Git(atlas.Root, "log", "-1", "--format=%H", "--", asking)?.Trim() is { Length: > 0 } c ? c : null;
            if (commit is null) return false;
            if (!newer.TryGetValue((commit, target), out var changed))
                newer[(commit, target)] = changed = Checkout.Git(atlas.Root, "rev-list", "--count", $"{commit}..HEAD", "--", target)?.Trim() is not (null or "0");
            return changed;
        }

        var items = new List<RetryItem>();
        foreach (var e in atlas.Entries.Values)
        {
            foreach (var q in QuestionList.Parse(e.Body).Items.Where(q => q.State != "dismissed"))
            {
                var wait = q.WaitingOn.Split(' ', 2, StringSplitOptions.TrimEntries);
                var target = wait.Length == 2 ? wait[1] : "";
                string? reason = null;
                if (q.Answer.Length > 0) reason = "a person answered; fold it in";
                else if (q.State != "open") continue;
                else if (wait[0] == "name" && providers[target].FirstOrDefault(id => id != e.Id) is { } provider) reason = $"{target} is now provided by {provider}";
                else if (wait[0] == "entry" && ChangedSince(e.Id + ".md", target + ".md")) reason = $"{target} has changed since this entry was last committed";
                else if (wait[0] == "repository" && ChangedSince(e.Id + ".md", "repositories/" + Scans.Folder(target))) reason = $"repository {Scans.Folder(target)} has changed since this entry was last committed";
                if (reason is not null) items.Add(new(e.Id, q.Id, reason, q.RaisedBy == "person"));
            }
        }
        return items.OrderBy(i => !i.Person).ToList();
    }
}
