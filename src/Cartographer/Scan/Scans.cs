using System.Text;

namespace Cartographer;

public sealed record DueRepository(string Folder, List<string> Reasons);

/// <summary>One repository's scan from start to finish: the mechanical pass, what is due, what changed, and the commit (spec sections 5 and 7).</summary>
public static class Scans
{
    /// <summary>The atlas folder name from what a person typed: a folder name, "repositories/x" or "repositories/x/repository".</summary>
    public static string Folder(string repository)
    {
        var parts = repository.Trim('/').Split('/');
        return Ids.Slug(parts[0] == "repositories" && parts.Length >= 2 ? parts[1] : repository);
    }

    static string Seven(string commit) => commit[..Math.Min(7, commit.Length)];
    static string Major(string version) => version.Split('.')[0];

    /// <summary>The mechanical pass for one repository, recorded as a scan in progress. Saves entries and state; the caller indexes and validates.</summary>
    public static string Run(Atlas atlas, string folder, string? path = null, bool full = false, IEnumerable<IExtractor>? extractors = null)
    {
        var state = ScanState.Load(atlas, folder);
        var repo = Checkout.Open(folder, path ?? Clones.Ensure(atlas, folder));
        var scanned = state.Default?.State.ScannedCommit;
        var isNew = atlas.Find(Ids.Repository(folder)) is null;

        var produced = Scanner.Run(atlas, repo, state.Entries, extractors);

        var entry = atlas.Entries[Ids.Repository(folder)];
        var locations = state.Locations.Where(l => !l.Gone).Select(l => new Location { Id = l.Id, Primary = l == state.Primary }).ToList();
        entry.Locations = locations.Count > 0 ? locations : null;
        if (isNew && state.Primary is { } primary) entry.Name = primary.Id.Split('/')[^1];

        var slugs = produced.Where(id => id.StartsWith($"repositories/{folder}/components/")).Select(id => id.Split('/')[^1]).ToList();
        var mode = full || scanned is null || Major(state.Cartographer) != Major(Clock.Version) ? "full" : "changed";
        var pending = slugs.ToHashSet();
        // The same commit scanned again carries on where an interrupted scan stopped.
        if (!full && state.InProgress is { } before && before.Commit == repo.Commit)
        {
            mode = before.Mode;
            pending.RemoveWhere(s => before.Components.GetValueOrDefault(s) == "done");
        }
        else if (mode == "changed")
        {
            // A last scanned commit the checkout does not have (history rewritten) cannot be compared: scan in full.
            if (Diff(repo, scanned!) is { } files) pending = ByComponent(repo, files, extractors).Keys.Where(id => id != "").Select(id => id.Split('/')[^1]).ToHashSet();
            else mode = "full";
        }

        var head = Checkout.Git(repo.Root, "rev-parse", "--abbrev-ref", "HEAD")?.Trim();
        state.InProgress = new InProgress
        {
            Branch = state.Primary?.DefaultBranch ?? state.Default?.Name ?? (head is null or "HEAD" ? "main" : head),
            Commit = repo.Commit,
            Mode = mode,
            Components = new(slugs.ToDictionary(s => s, s => pending.Contains(s) ? "pending" : "done"), StringComparer.Ordinal),
        };
        state.Entries = produced.Order(StringComparer.Ordinal).ToList();
        state.Cartographer = Clock.Version;
        state.Save(atlas);
        var changed = atlas.Save();
        return $"{folder} at {repo.Commit7}: {mode} scan, {produced.Count} entries produced, {changed.Count} files changed, {state.InProgress.Components.Count(c => c.Value == "pending")} of {slugs.Count} components to revisit";
    }

    /// <summary>Files that differ between a commit and the checkout's commit, or null when git cannot compare them.</summary>
    static List<string>? Diff(Checkout repo, string from) =>
        Checkout.Git(repo.Root, "-c", "core.quotepath=off", "diff", "--name-only", from, repo.Commit)?.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Files grouped by the ID of the component that owns them; "" holds those outside any component.</summary>
    static SortedDictionary<string, List<string>> ByComponent(Checkout repo, IEnumerable<string> files, IEnumerable<IExtractor>? extractors)
    {
        // ponytail: runs the extractors again just for the component roots; have Scanner.Run return them if this ever shows up in timings.
        var x = new Extraction(repo);
        foreach (var extractor in extractors ?? Extractors.All) extractor.Extract(x);
        var groups = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var id = x.ComponentAt(file)?.Id ?? "";
            if (!groups.TryGetValue(id, out var list)) groups[id] = list = [];
            list.Add(file);
        }
        return groups;
    }

    /// <summary>Files changed since the last scanned commit, grouped by component.</summary>
    public static string Changed(Atlas atlas, string folder, string? path = null, IEnumerable<IExtractor>? extractors = null)
    {
        if (ScanState.Load(atlas, folder).Default?.State.ScannedCommit is not { } scanned) return $"{folder} has never been scanned: everything is new\n";
        var repo = Checkout.Open(folder, path ?? Clones.Ensure(atlas, folder));
        var files = Diff(repo, scanned) ?? throw new UsageException($"the checkout at {repo.Root} does not have the last scanned commit {Seven(scanned)}; scan with --full");
        var sb = new StringBuilder($"{folder}: {files.Count} file(s) changed between {Seven(scanned)} and {repo.Commit7}\n");
        foreach (var (id, list) in ByComponent(repo, files, extractors).OrderBy(g => g.Key == ""))
        {
            sb.Append(id == "" ? "outside any component\n" : id + "\n");
            foreach (var file in list) sb.Append($"  {file}\n");
        }
        return sb.ToString();
    }

    /// <summary>Every repository with a state file that is due, with its reasons: interrupted scans, then never scanned, then the rest by folder name.</summary>
    public static List<DueRepository> Due(Atlas atlas)
    {
        var retry = QuestionRetry.Find(atlas);
        var due = new List<(int Rank, DueRepository Repository)>();
        foreach (var folder in ScanState.All(atlas))
        {
            var state = ScanState.Load(atlas, folder);
            var last = state.Default?.State;
            var reasons = new List<string>();
            if (state.InProgress is { } ip) reasons.Add($"a scan of {Seven(ip.Commit)} was started and not finished");
            if (last?.ScannedCommit is null) reasons.Add("never scanned");
            else
            {
                if (state.Primary?.Tip is { } tip && tip != last.ScannedCommit) reasons.Add($"new commits: tip {Seven(tip)}, scanned {Seven(last.ScannedCommit)}");
                if (Major(state.Cartographer) != Major(Clock.Version)) reasons.Add($"scanned with Cartographer {state.Cartographer}, now {Clock.Version}");
                if (DateTime.TryParse(last.Scanned, out var when) && (DateTime.Parse(Clock.Today) - when).TotalDays > atlas.Config.Limits.ScanAgeDays)
                    reasons.Add($"last scanned on {last.Scanned}, more than {atlas.Config.Limits.ScanAgeDays} days ago");
            }
            var questions = retry.Count(r => r.Entry.StartsWith($"repositories/{folder}/") || state.Entries.Contains(r.Entry));
            if (questions > 0) reasons.Add($"{questions} open question(s) can be retried");
            if (reasons.Count > 0) due.Add((state.InProgress is not null ? 0 : last?.ScannedCommit is null ? 1 : 2, new(folder, reasons)));
        }
        return due.OrderBy(d => d.Rank).ThenBy(d => d.Repository.Folder, StringComparer.Ordinal).Select(d => d.Repository).ToList();
    }

    /// <summary>
    /// Marks a repository's scan complete, writes its change summary and commits the scan's files on the scan branch.
    /// Code 1 with the errors listed when an entry of this repository is invalid; nothing is changed then.
    /// </summary>
    public static (int Code, string Text) Finish(Atlas atlas, string folder)
    {
        var branch = Sessions.Branch(atlas.Root);
        if (branch is "main" or "master") throw new UsageException($"refusing to commit on {branch}. Start a scan branch first: cartographer session start");
        var state = ScanState.Load(atlas, folder);
        var ip = state.InProgress ?? throw new UsageException($"{folder} has no scan in progress. Run: cartographer scan {folder}");

        var prefix = $"repositories/{folder}/";
        var errors = Validator.Check(atlas).Where(i => i.Error && (i.Id.StartsWith(prefix) || state.Entries.Contains(i.Id))).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        if (errors.Count > 0) return (1, string.Join('\n', errors) + $"\n{folder} is not finished: fix the {errors.Count} error(s) above and run finish again\n");

        foreach (var other in state.Branches.Where(b => b.Value.Role == "default" && b.Key != ip.Branch).Select(b => b.Key).ToList()) state.Branches.Remove(other);
        state.Branches[ip.Branch] = new BranchState { ScannedCommit = ip.Commit, Scanned = Clock.Today };
        state.InProgress = null;
        state.Save(atlas);
        Indexer.Write(atlas);

        // This scan's files: everything changed in the atlas's own folders, except what belongs to another scan still in progress.
        var others = ScanState.All(atlas).Where(f => f != folder).Select(f => ScanState.Load(atlas, f)).Where(s => s.InProgress is not null).ToList();
        var theirFolders = others.Select(s => Ids.Area(s.Repository) + "/").ToList();
        var theirEntries = others.SelectMany(s => s.Entries).Except(state.Entries).Select(id => id + ".md").ToHashSet();
        var status = Shell.Git(atlas.Root, null, "status", "--porcelain", "-z", "--no-renames", "--untracked-files=all")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(r => (IsNew: r[..2] is "??" or "A ", Path: r[3..]))
            .Where(r => r.Path.StartsWith(Indexer.Folder + "/")
                || Atlas.Folders.Any(f => r.Path.StartsWith(f + "/")) && !theirFolders.Any(r.Path.StartsWith) && !theirEntries.Contains(r.Path))
            .ToList();

        var commit7 = Seven(ip.Commit);
        var entries = status.Where(r => r.Path.EndsWith(".md") && !r.Path.StartsWith(Indexer.Folder + "/")).Select(r => (Id: r.Path[..^3], r.IsNew));
        var summary = ChangeSummary.Build(atlas, folder, commit7, entries);
        var summaryPath = $"changes/{Clock.Today}-{folder}-{commit7}.md";
        Directory.CreateDirectory(Path.Combine(atlas.Root, "changes"));
        File.WriteAllText(Path.Combine(atlas.Root, summaryPath), summary);

        var paths = string.Join('\0', status.Select(r => r.Path).Append(summaryPath));
        Shell.Git(atlas.Root, paths, "--literal-pathspecs", "add", "-A", "--pathspec-from-file=-", "--pathspec-file-nul");
        var message = $"Scan {folder} at {commit7}\n\n{summary[(summary.IndexOf('\n') + 1)..].Trim('\n')}\n";
        Shell.Git(atlas.Root, paths, "--literal-pathspecs", "commit", "-q", "-m", message, "--pathspec-from-file=-", "--pathspec-file-nul");
        return (0, $"finished {folder} at {commit7}: {status.Count + 1} files committed on {branch}\n{summary.Split('\n')[2]}\n");
    }
}
