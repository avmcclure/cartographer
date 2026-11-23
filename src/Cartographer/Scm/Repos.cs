using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>One repository and the locations that hold it, primary first. <c>Question</c> is set when a look-alike could not be linked.</summary>
public sealed record LinkedRepo(string Folder, List<RemoteRepo> Locations, string? Question = null, string? LookAlike = null);

/// <summary>What a refresh found: repositories written to scan state, and those left out.</summary>
public sealed record Listing(List<LinkedRepo> Repositories, List<RemoteRepo> Skipped, List<string> Excluded);

/// <summary>Listing repositories at both SCMs, linking their locations and recording them in scan state.</summary>
public static class Repos
{
    /// <summary>Calls both SCMs, links locations, and updates every repository's scan state. Saves.</summary>
    public static async Task<Listing> Refresh(Atlas atlas, Scm scm)
    {
        var config = atlas.Config;
        var githubOrgs = Orgs(config.Github, config.Allow.Github.Keys);
        var azdoOrgs = Orgs(config.AzureDevops, config.Allow.AzureDevops.Keys);
        if (githubOrgs.Count == 0 && azdoOrgs.Count == 0)
            throw new UsageException($"{Atlas.ConfigFile} names no SCM. Add 'github: {{organisation: <name>}}' or 'azure-devops: {{organisation: <name>}}', or organisations under 'allow'.");
        CheckPatterns(config);
        bool Allowed(string id) => IsAllowed(config, id);

        var remotes = new List<RemoteRepo>();
        foreach (var org in azdoOrgs)
        {
            // Without a sign-in only named projects can be read: those in a locations mapping, and plain names under allow.
            var prefix = $"azdo:{org}/";
            var mapped = config.Locations.Values.SelectMany(ids => ids).Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && id.Split('/').Length == 3).Select(id => id.Split('/')[1]);
            var named = config.Allow.AzureDevops.Where(o => Same(o.Key, org) && o.Value is not null).SelectMany(o => o.Value!.Keys).Where(k => !IsRegex(k) && k.IndexOfAny(['*', '?']) < 0);
            remotes.AddRange(await scm.AzureDevOpsRepos(org, mapped.Concat(named), Allowed));
        }
        foreach (var org in githubOrgs) remotes.AddRange(await scm.GitHubRepos(org, Allowed));

        var skipped = remotes.Where(r => r.Skipped is not null).ToList();
        var linked = await Link(atlas, remotes.Except(skipped).ToList(), scm);
        var excluded = linked.Where(l => IsExcluded(config, l.Folder)).ToList();
        var kept = linked.Except(excluded).ToList();

        foreach (var repo in kept)
        {
            Record(atlas, repo);
            if (repo.Question is not null && atlas.Find(Ids.Repository(repo.Folder)) is { } entry)
                QuestionList.Update(entry, list =>
                {
                    // A person who dismissed it by naming the look-alike is not asked again.
                    if (!list.Dismissed.Any(d => d.Contains(repo.LookAlike!, StringComparison.OrdinalIgnoreCase)))
                        list.Add(repo.Question, "person", $"A locations mapping in {Atlas.ConfigFile} naming both, or a statement that they are separate repositories.", "cli");
                });
        }
        // A repository nothing lists any more: every location is gone. Excluded and no longer allowed ones are left as they are.
        foreach (var folder in ScanState.All(atlas).Where(f => !IsExcluded(config, f) && kept.All(k => k.Folder != f) && ScanState.Load(atlas, f).Locations.All(l => Allowed(l.Id))).ToList())
            Record(atlas, new(folder, []));
        atlas.Save();
        return new(kept, skipped, excluded.Select(e => e.Folder).ToList());
    }

    /// <summary>
    /// Groups locations into repositories. Evidence, in order: a mapping in the config; a GitHub migration record
    /// naming the Azure DevOps source; the &lt;project&gt;-&lt;repo&gt; naming rule confirmed by a shared commit.
    /// A repository already in the atlas keeps its folder.
    /// </summary>
    public static async Task<List<LinkedRepo>> Link(Atlas atlas, List<RemoteRepo> remotes, Scm scm)
    {
        var config = atlas.Config;
        var left = remotes.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        var homes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in ScanState.All(atlas))
            foreach (var l in ScanState.Load(atlas, folder).Locations.Where(l => !l.Gone)) homes.TryAdd(l.Id, folder);
        string? Home(params RemoteRepo[] members) => members.Select(m => homes.GetValueOrDefault(m.Id)).FirstOrDefault(h => h is not null);

        // Folders are handed out in this order, so on a clash the earlier group keeps the plain name:
        // mapped by a person, already in the atlas, holding an Azure DevOps location, GitHub only.
        var linked = new List<(int Rank, LinkedRepo Repo)>();
        void Add(string folder, string? question, string? lookAlike, params RemoteRepo[] members) =>
            linked.Add((Home(members) is not null ? 1 : members.Any(m => !m.IsGitHub) ? 2 : 3, new(Home(members) ?? folder, [.. members], question, lookAlike)));

        foreach (var (key, ids) in config.Locations.OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            var members = left.Where(r => ids.Contains(r.Id, StringComparer.OrdinalIgnoreCase)).OrderBy(r => !r.IsGitHub).ToList();
            if (members.Count == 0) continue;
            left.RemoveAll(members.Contains);
            var parts = key.Split('/');
            linked.Add((0, new(Ids.Slug(parts[0] == "repositories" && parts.Length > 1 ? parts[1] : key), members)));
        }

        var azdo = left.Where(r => !r.IsGitHub).ToList();
        var github = left.Where(r => r.IsGitHub).ToList();
        // Migration records, keyed by the GitHub location ID they produced.
        var migrations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (azdo.Count > 0)
            foreach (var org in github.Select(r => r.Id["github:".Length..].Split('/')[0]).Distinct(StringComparer.OrdinalIgnoreCase))
                foreach (var (name, url) in await scm.GitHubMigrations(org)) migrations[$"github:{org}/{name}"] = url;
        const string noCommit = "the names match but no commit is shared.";
        foreach (var repo in github)
        {
            var twin = migrations.TryGetValue(repo.Id, out var source)
                ? azdo.FirstOrDefault(a => new[] { source, Uri.UnescapeDataString(source) }.Any(s => Same(Scm.CloneUrl(a.Id), s.TrimEnd('/')) || Same(Uri.UnescapeDataString(Scm.CloneUrl(a.Id)), s.TrimEnd('/'))))
                : null;
            // The naming rule. A GitHub name equal to <project>-<repo> is the same thing as equal folder names; when a
            // hyphen makes two projects fit (Finance-Core/ledger and Finance/Core-ledger), the longest project name wins.
            var named = twin is null ? azdo.Where(a => a.Folder == repo.Folder).OrderByDescending(a => a.Project!.Length).FirstOrDefault() : null;
            if (named is not null && (repo.Tip == named.Tip || await scm.HasCommit(repo, named.Tip!) || await scm.HasCommit(named, repo.Tip!))) twin = named;

            if (twin is not null)
            {
                azdo.Remove(twin);
                Add(twin.Folder, null, null, repo, twin);
            }
            else if (named is not null)
                Add(repo.Folder, $"Whether {repo.Id} is the same repository as {named.Id}: {noCommit}", named.Id, repo);
            else if (Home(repo) is null && File.Exists(ScanState.PathOf(atlas, repo.Folder)))
            {
                // The folder holds a repository whose locations are no longer listed, typically an Azure DevOps copy
                // disabled after its migration. A commit they share makes this its GitHub location; otherwise it is a look-alike.
                var old = ScanState.Load(atlas, repo.Folder).Locations.Where(l => l.Tip is not null).ToList();
                var shared = old.Count == 0;
                foreach (var l in old) if (shared = l.Tip == repo.Tip || await scm.HasCommit(repo, l.Tip!)) break;
                if (shared) Add(repo.Folder, null, null, repo);
                else Add(repo.Folder + "-github", $"Whether {repo.Id} is the same repository as {old[0].Id}: {noCommit}", old[0].Id, repo);
            }
            else Add(repo.Folder, null, null, repo);
        }
        foreach (var a in azdo) Add(a.Folder, null, null, a);

        // Two repositories never share a folder: a GitHub repository that only looks like another gets "-github", anything else a number.
        var used = new HashSet<string>(StringComparer.Ordinal);
        return linked.OrderBy(l => l.Rank).Select(l => l.Repo).Select(l =>
        {
            if (used.Add(l.Folder)) return l;
            var stem = l.Locations.All(r => r.IsGitHub) ? l.Folder + "-github" : l.Folder;
            var folder = stem;
            for (var n = 2; !used.Add(folder); n++) folder = $"{stem}-{n}";
            return l with { Folder = folder };
        }).OrderBy(l => l.Folder, StringComparer.Ordinal).ToList();
    }

    /// <summary>A config exclude pattern matches the folder name.</summary>
    public static bool IsExcluded(AtlasConfig config, string folder) => config.Exclude.Any(p => Matches(p, folder));

    /// <summary>
    /// Whether the allow section lets a location in: its organisation is named, and its project (Azure DevOps) and
    /// repository match. An SCM with nothing under allow lets everything in.
    /// </summary>
    public static bool IsAllowed(AtlasConfig config, string locationId)
    {
        var p = locationId[(locationId.IndexOf(':') + 1)..].Split('/');
        static bool AnyMatch(ICollection<string>? patterns, string name) => patterns is null or { Count: 0 } || patterns.Any(x => Matches(x, name));
        var allow = config.Allow;
        return locationId.StartsWith("github:")
            ? allow.Github.Count == 0 || allow.Github.Any(o => Same(o.Key, p[0]) && AnyMatch(o.Value, p[1]))
            : allow.AzureDevops.Count == 0 || allow.AzureDevops.Any(o => Same(o.Key, p[0])
                && (o.Value is null or { Count: 0 } || o.Value.Any(project => Matches(project.Key, p[1]) && AnyMatch(project.Value, p[2]))));
    }

    /// <summary>
    /// A name pattern: <c>/regex/</c>, which matches anywhere unless anchored, or else a glob where * is any run of
    /// characters and ? is one, matching the whole name. Case is ignored.
    /// </summary>
    public static bool Matches(string pattern, string name) => Regex.IsMatch(name, ToRegex(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static bool IsRegex(string pattern) => pattern.Length > 1 && pattern[0] == '/' && pattern[^1] == '/';
    static string ToRegex(string pattern) => IsRegex(pattern) ? pattern[1..^1] : "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";

    static void CheckPatterns(AtlasConfig config)
    {
        var none = Enumerable.Empty<string>();
        var patterns = config.Exclude
            .Concat(config.Allow.Github.Values.SelectMany(r => r ?? none))
            .Concat(config.Allow.AzureDevops.Values.OfType<Dictionary<string, List<string>?>>().SelectMany(o => o).SelectMany(p => (p.Value ?? none).Append(p.Key)));
        foreach (var p in patterns)
            try { _ = new Regex(ToRegex(p)); }
            catch (ArgumentException e) { throw new UsageException($"'{p}' in {Atlas.ConfigFile} is not a regular expression: {e.Message}"); }
    }

    /// <summary>The organisations to list at one SCM: the one it names, then those under allow.</summary>
    public static List<string> Orgs(ScmConfig? scm, IEnumerable<string> allowed) =>
        new[] { scm?.Organisation }.Concat(allowed).OfType<string>().Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Writes a repository's locations into its scan state. The first location is primary; stored ones not passed are marked gone.</summary>
    static void Record(Atlas atlas, LinkedRepo repo)
    {
        var state = ScanState.Load(atlas, repo.Folder);
        foreach (var stored in state.Locations) stored.Primary = false;
        foreach (var stored in state.Locations.Where(s => repo.Locations.All(r => !Same(r.Id, s.Id)))) stored.Gone = true;
        foreach (var r in repo.Locations)
        {
            var l = state.Locations.FirstOrDefault(s => Same(s.Id, r.Id));
            if (l is null) state.Locations.Add(l = new LocationState { Id = r.Id });
            (l.Primary, l.Gone, l.DefaultBranch, l.Tip, l.Seen) = (r == repo.Locations[0], false, r.DefaultBranch, r.Tip, Clock.Today);
        }
        if (state.Default is null && state.Primary?.DefaultBranch is { } branch) state.Branches[branch] = new BranchState();
        state.Save(atlas);
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>new (never scanned), changed (the primary's tip is not the scanned commit), unchanged, or gone (no location is left).</summary>
    public static string StateOf(ScanState state) =>
        state.Primary is not { } primary ? "gone"
        : state.Default?.State.ScannedCommit is not { } scanned ? "new"
        : scanned == primary.Tip ? "unchanged" : "changed";

    /// <summary>The stored scan states as a table: folder, locations, default branch, tip, state.</summary>
    public static string Table(Atlas atlas)
    {
        var rows = new List<string[]> { new[] { "FOLDER", "LOCATIONS", "BRANCH", "TIP", "STATE" } };
        foreach (var folder in ScanState.All(atlas))
        {
            var s = ScanState.Load(atlas, folder);
            var shown = s.Primary ?? s.Locations.FirstOrDefault();
            rows.Add([folder, string.Join(", ", s.Locations.Select(l => l.Id + (l.Gone ? " (gone)" : ""))), shown?.DefaultBranch ?? "", shown?.Tip is { } tip ? tip[..Math.Min(7, tip.Length)] : "", StateOf(s)]);
        }
        if (rows.Count == 1) return "no repositories are known yet. Run: cartographer repos list --refresh\n";
        var widths = Enumerable.Range(0, 5).Select(i => rows.Max(r => r[i].Length)).ToArray();
        var sb = new StringBuilder();
        foreach (var r in rows) sb.Append(string.Join("  ", r.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd()).Append('\n');
        return sb.ToString();
    }
}
