using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>
/// Writes everything the site is built from (spec section 8) as JSON files. The shapes are described in
/// site/README.md. Output depends only on the atlas and its pinned clusters: sorted, no timestamps.
/// </summary>
public static class SitePrepare
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, NewLine = "\n", PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The names of what prepare writes directly under the output directory; anything else there is left alone.</summary>
    static readonly string[] Owned = ["entries", "sheets", "site.json", "clusters.json", "everything.json", "gazetteer.json", "graph.json", "questions.json", "changes.json", "tags.json"];

    /// <param name="editUrl">Where an atlas file can be edited, e.g. https://github.com/acme/atlas/edit/main; null for no edit links.</param>
    public static Grouping Run(Atlas atlas, string outDir, string? editUrl = null)
    {
        var grouping = Clusters.Current(atlas);
        var keys = Keys(grouping.Clusters);
        var clusterOf = grouping.Clusters.SelectMany(c => c.Members.Select(m => (m, key: keys[c]))).ToDictionary(x => x.m, x => x.key, StringComparer.Ordinal);
        string? PartOf(string id) => Clusters.PartOf(atlas, id);
        string? ClusterOf(string id) => PartOf(id) is { } part ? clusterOf.GetValueOrDefault(part) : null;

        Directory.CreateDirectory(outDir);
        foreach (var name in Owned)
        {
            var path = Path.Combine(outDir, name);
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else File.Delete(path);
        }
        void Write(string path, object value)
        {
            var full = Path.Combine(outDir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, JsonSerializer.Serialize(value, Json) + "\n");
        }

        // Links between entries that both exist, as the site draws and selects them.
        var edges = atlas.Entries.Values.SelectMany(e => (e.Links ?? []).Where(l => l.To is not null && l.To != e.Id && atlas.Find(l.To) is not null)
            .Select(l => (From: e.Id, To: l.To!, l.Type, l.Basis, l.How, Ops: l.Type is "depends-on" or "uses" ? null : l.Uses))).ToList();

        var inbound = Indexer.Inbound(atlas);
        var questions = atlas.Entries.Values.ToDictionary(e => e.Id, e => QuestionList.Parse(e.Body), StringComparer.Ordinal);
        var latest = atlas.Entries.Values.Select(e => e.Verified?.Date).Where(d => d is not null).Max(StringComparer.Ordinal);
        bool Stale(Entry e) => e.Verified is { } v && latest is not null && DateTime.TryParse(v.Date, out var d) && (DateTime.Parse(latest) - d).TotalDays > atlas.Config.Limits.VerifiedAgeDays;
        object? Ref(string? id) => id is null ? null : atlas.Find(id) is { } e ? new { id, e.Name, e.Kind, e.Type, retired = e.Status == "retired" ? true : (bool?)null } : new { id, Name = id, Kind = "missing", Type = (string?)null, retired = (bool?)null };
        string? Edit(string id) => editUrl is null ? null : $"{editUrl.TrimEnd('/')}/{id}.md";

        foreach (var e in atlas.Entries.Values)
        {
            EvidenceLink? Ev(string? source) => Evidences.Of(atlas, e, source);
            var usedBy = (inbound.GetValueOrDefault(e.Id) ?? []).Distinct()
                .OrderBy(l => l.Type, StringComparer.Ordinal).ThenBy(l => l.From, StringComparer.Ordinal).ThenBy(l => l.Basis, StringComparer.Ordinal)
                .Select(l =>
                {
                    var from = atlas.Entries[l.From];
                    var source = l.Type == "member" ? from.Systems?.FirstOrDefault(s => s.Id == e.Id && s.Basis == l.Basis)?.Source
                        : from.Links?.FirstOrDefault(x => x.Type == l.Type && x.To == e.Id && x.Basis == l.Basis)?.Source;
                    return new { l.Type, heading = Indexer.Reverse(l.Type), from = Ref(l.From), l.Basis, evidence = Evidences.Of(atlas, from, source) };
                }).ToList();
            Write($"entries/{e.Id}.json", new
            {
                e.Id, e.Kind, e.Type, e.Name, e.Summary, e.Status,
                repository = Ref(e.Repository), database = Ref(e.Database), e.Engine, e.Stack, e.Aliases, e.Instances, e.Packages,
                locations = e.Locations?.Select(l => new { l.Id, l.Primary, url = Evidences.Home(l.Id) }),
                provides = e.Provides?.Select(p => new { p.Name, p.As, p.Basis, evidence = Ev(p.Source) }),
                systems = e.Systems?.Select(s => new { system = Ref(s.Id), s.Basis, evidence = Ev(s.Source) }),
                tags = e.Tags?.Select(t => new { t.Name, t.Basis, evidence = Ev(t.Source) }),
                owner = e.Owner is { } o ? new { o.Name, o.Basis, evidence = Ev(o.Source) } : null,
                role = e.Role is { } r ? new { r.Name, r.Basis, evidence = Ev(r.Source) } : null,
                uses = e.Links?.Select(l => new { l.Type, verb = Verb(l.Type), to = Ref(l.To), unresolved = l.To is null ? l.Name : null, l.Name, l.Database, l.Version, l.How, via = Ref(l.Via), names = l.Uses, l.Count, l.Basis, evidence = Ev(l.Source) }),
                usedBy,
                drift = e.Drift?.Select(d => new { d.What, sources = d.Sources.Select(Ev) }),
                e.Verified, stale = Stale(e) ? true : (bool?)null,
                sections = Sections.Split(e.Body).Where(s => s.Title != Sections.Questions && s.Text.Trim().Length > 0).Select(s => new { s.Title, markdown = s.Text }),
                questions = questions[e.Id].Items.Select(q => new { q.Id, q.State, q.RaisedBy, q.Attempts, q.Missing, q.WaitingOn, q.AnswerLooksLike, q.Answer }),
                dismissed = questions[e.Id].Dismissed.Count > 0 ? questions[e.Id].Dismissed : null,
                part = PartOf(e.Id) is { } part && part != e.Id ? Ref(part) : null, cluster = ClusterOf(e.Id), editUrl = Edit(e.Id),
            });
        }

        int Unsurveyed(Entry e) => questions[e.Id].Items.Count(q => q.State is "open" or "needs-person");
        Write("everything.json", atlas.Entries.Values.Select(e => new
        {
            e.Id, e.Name, e.Kind, e.Type, e.Summary, e.Status, cluster = ClusterOf(e.Id),
            unsurveyed = Unsurveyed(e), verified = e.Verified?.Date, stale = Stale(e) ? true : (bool?)null,
        }));

        // The map: clusters, and the links between parts in the direction the data goes, with what they do, counted.
        // Calls carry requests rather than data and keep their own direction; code dependencies are left out.
        // A pass-through carries data whichever way its callers move it: what is written into it goes on to its targets,
        // and what is read from it comes back from them, through any chain of pass-throughs. With no callers known, a
        // forward points onward.
        var into = edges.Where(e => e.Type is not ("depends-on" or "forwards-to") && Flow(e.Type) != 0).GroupBy(e => e.To).ToDictionary(g => g.Key, g => g.Select(e => Flow(e.Type)).ToHashSet(), StringComparer.Ordinal);
        var forwardedBy = edges.Where(e => e.Type == "forwards-to").GroupBy(e => e.To).ToDictionary(g => g.Key, g => g.Select(e => e.From).ToList(), StringComparer.Ordinal);
        // ponytail: walked afresh for each forward, no caching (a cache goes wrong in a loop of pass-throughs); cache per
        // strongly connected group if an atlas ever has thousands of them.
        HashSet<int> WaysThrough(string proxy, HashSet<string> seen)
        {
            var found = new HashSet<int>(into.GetValueOrDefault(proxy) ?? []);
            if (seen.Add(proxy)) foreach (var outer in forwardedBy.GetValueOrDefault(proxy) ?? []) found.UnionWith(WaysThrough(outer, seen));
            return found;
        }
        IEnumerable<int> Ways(string from, string type) => type != "forwards-to" ? [Flow(type)]
            : WaysThrough(from, new(StringComparer.Ordinal)) is { Count: > 0 } w ? w.OrderDescending() : [1];
        var partLinks = edges.Where(e => e.Type != "depends-on")
            .SelectMany(e => Ways(e.From, e.Type).Select(flow => (From: PartOf(e.From), To: PartOf(e.To), e.Type, e.Basis, e.How, e.Ops, Flow: flow, Back: e.Type == "forwards-to" && flow < 0)))
            .Where(e => e.From is not null && e.To is not null && e.From != e.To)
            .Select(e => e.Flow < 0 ? e with { From = e.To, To = e.From } : e)
            // Data read back through a pass-through is kept apart from ordinary links between the same two parts.
            .GroupBy(e => (e.From!, e.To!, Call: e.Flow == 0, e.Back)).OrderBy(g => g.Key.Item1, StringComparer.Ordinal).ThenBy(g => g.Key.Item2, StringComparer.Ordinal).ThenBy(g => g.Key.Call).ThenBy(g => g.Key.Back)
            .Select(g => new
            {
                from = g.Key.Item1, to = g.Key.Item2, call = g.Key.Call ? true : (bool?)null, count = g.Count(), basis = Weakest(g.Select(x => x.Basis)),
                verbs = g.GroupBy(x => Verb(x.Type)).Select(v => new { verb = v.Key, count = v.Count() }).OrderByDescending(v => v.count).ThenBy(v => v.verb, StringComparer.Ordinal),
                // Transports, with "" counting links that record none, so the map can filter on either.
                over = g.Any(x => x.How is not null) ? g.GroupBy(x => x.How?.ToLowerInvariant() ?? "").Select(h => new { how = h.Key, count = h.Count() }).OrderBy(h => h.how == "").ThenByDescending(h => h.count).ThenBy(h => h.how, StringComparer.Ordinal) : null,
                ops = g.Any(x => x.Ops is not null) ? g.SelectMany(x => x.Ops ?? []).Distinct().Order(StringComparer.Ordinal) : null,
                forwards = g.Any(x => x.Type == "forwards-to") ? true : (bool?)null,
                back = g.Key.Back ? true : (bool?)null,
            }).ToList();
        var forwarders = edges.Where(e => e.Type == "forwards-to").Select(e => PartOf(e.From)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var roles = Roles(atlas, grouping, forwarders, partLinks.Select(l => (l.from, l.to, Call: l.call == true, Back: l.back == true, Verbs: l.verbs.Select(v => v.verb))).ToList());
        var questionsOf = atlas.Entries.Values.Where(e => PartOf(e.Id) is not null).GroupBy(e => PartOf(e.Id)!).ToDictionary(g => g.Key, g => g.Sum(Unsurveyed), StringComparer.Ordinal);
        Write("clusters.json", new
        {
            links = grouping.Links, inside = grouping.Inside,
            clusters = grouping.Clusters.Select(c => new
            {
                key = keys[c], c.Name, c.Pinned, unlinked = c.Name == Clusters.Unlinked ? true : (bool?)null,
                members = c.Members.Select(m => new
                {
                    id = m, atlas.Entries[m].Name, atlas.Entries[m].Kind, atlas.Entries[m].Type, found = Found(atlas, atlas.Entries[m]), unsurveyed = questionsOf.GetValueOrDefault(m),
                    role = roles.TryGetValue(m, out var r) && r.Name != "stage" ? r.Name : null, roleStated = r.Stated && r.Name != "stage" ? true : (bool?)null,
                }),
            }),
            parts = partLinks,
            suggestion = grouping.Suggestion is { } s ? new
            {
                s.Inside,
                moved = s.Moved.Select(m => new { part = Ref(m.Part), m.From, m.To }),
                gone = grouping.Clusters.Where(c => c.Pinned && s.Clusters.All(f => f.Name != c.Name)).Select(c => c.Name),
            } : null,
        });

        Write("questions.json", atlas.Entries.Values.SelectMany(e => questions[e.Id].Items.Select(q => new
        {
            entry = Ref(e.Id), q.Id, q.State, q.RaisedBy, q.Attempts, q.Missing, q.WaitingOn, q.AnswerLooksLike, q.Answer, editUrl = Edit(e.Id),
        })));

        var changes = Path.Combine(atlas.Root, "changes");
        Write("changes.json", (Directory.Exists(changes) ? Directory.EnumerateFiles(changes, "*.md") : [])
            .Select(f => (File: f, Match: Regex.Match(Path.GetFileName(f), @"^(\d{4}-\d{2}-\d{2})-(.+)-([0-9a-f]{7})\.md$"))).Where(x => x.Match.Success)
            .OrderByDescending(x => Path.GetFileName(x.File), StringComparer.Ordinal)
            .Select(x =>
            {
                var text = File.ReadAllText(x.File).Replace("\r\n", "\n").Trim('\n');
                var title = text.StartsWith("# ") ? text.Split('\n')[0][2..].Trim() : null;
                var repository = Ids.Repository(x.Match.Groups[2].Value);
                return new
                {
                    date = x.Match.Groups[1].Value, repository = Ref(repository), commit = x.Match.Groups[3].Value,
                    title = title ?? $"{x.Match.Groups[2].Value} at {x.Match.Groups[3].Value}",
                    markdown = title is null ? text : text[(text.IndexOf('\n') + 1)..].Trim('\n'),
                };
            }));

        // Business-data tags: the tables that carry each, and the clusters those tables are in.
        Write("tags.json", atlas.OfKind(Kinds.Table, Kinds.View).SelectMany(e => (e.Tags ?? []).Select(t => (Tag: t, Entry: e)))
            .GroupBy(x => x.Tag.Name).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new
            {
                name = g.Key,
                tables = g.OrderBy(x => x.Entry.Id, StringComparer.Ordinal).Select(x => new { entry = Ref(x.Entry.Id), x.Tag.Basis, evidence = Evidences.Of(atlas, x.Entry, x.Tag.Source) }),
                clusters = g.Select(x => ClusterOf(x.Entry.Id)).OfType<string>().Distinct().Order(StringComparer.Ordinal),
            }));

        Write("site.json", new
        {
            organisation = atlas.Config.Organisation, cartographer = Clock.Version, latestVerified = latest, staleAfterDays = atlas.Config.Limits.VerifiedAgeDays,
            editable = editUrl is not null,
        });
        return grouping;
    }

    public static string Verb(string type) => type.Replace('-', ' ');

    /// <summary>
    /// Which way data goes along a link (spec section 8): 1 from the linking entry to its target, -1 the other way, 0
    /// for a link that relates two parts without moving data one way: calls, uses, links between databases, foreign
    /// keys and triggers. Code dependencies never reach here, and defines starts at a repository, which is not a part.
    /// </summary>
    public static int Flow(string type) => type switch
    {
        "writes" or "publishes-to" or "sends-to" or "forwards-to" => 1,
        "reads" or "consumes-from" or "receives-from" => -1,
        "calls" or "uses" or "links-to" or "references" or "fires-on" => 0,
        _ => 0,   // ponytail: a link type added later is drawn as a call until it is placed here
    };

    /// <summary>
    /// Where each part sits (spec section 8). A pass-through forwards requests to other parts. A lookup is a component or
    /// external system that two or more parts read from, whose data comes only from databases, so it sits beside the
    /// flow rather than in it. A role an entry states wins. Parts with neither are stages, in the flow.
    /// </summary>
    static Dictionary<string, (string Name, bool Stated)> Roles(Atlas atlas, Grouping grouping, HashSet<string> forwarders, List<(string From, string To, bool Call, bool Back, IEnumerable<string> Verbs)> links)
    {
        var roles = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
        // Who reads a part, directly or through pass-throughs in front of it.
        HashSet<string> Readers(string id, HashSet<string> seen)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            if (!seen.Add(id)) return found;
            foreach (var l in links.Where(l => l.From == id && !l.Call))
                if (l.Back) found.UnionWith(Readers(l.To, seen));
                else if (l.Verbs.Any(v => v is "reads" or "receives from")) found.Add(l.To);
            return found;
        }
        foreach (var id in grouping.Clusters.SelectMany(c => c.Members))
        {
            var e = atlas.Entries[id];
            // A stated role counts only where check would accept it.
            if (e.Role is { } stated && LinkTypes.Roles.Contains(stated.Name) && e.Kind is Kinds.Component or Kinds.External) { roles[id] = (stated.Name, true); continue; }
            if (forwarders.Contains(id)) { roles[id] = ("pass-through", false); continue; }
            var into = links.Where(l => l.To == id && !l.Call).ToList();
            var readers = Readers(id, new(StringComparer.Ordinal)).Count;
            if (e.Kind is Kinds.Component or Kinds.External && readers >= 2 && into.All(l => atlas.Find(l.From)?.Kind == Kinds.Database))
                roles[id] = ("lookup", false);
        }
        return roles;
    }

    static string Weakest(IEnumerable<string> bases)
    {
        var all = bases.ToHashSet(StringComparer.Ordinal);
        return all.Contains(Basis.Inferred) ? Basis.Inferred : all.Contains(Basis.Extracted) ? Basis.Extracted : Basis.Stated;
    }

    /// <summary>Where a part was found: its repository or database, by name. Shown, never used for grouping.</summary>
    static string? Found(Atlas atlas, Entry e) => e.Kind switch
    {
        Kinds.Component => atlas.Find(e.Repository ?? Ids.Area(e.Id) + "/repository")?.Name,
        Kinds.Database => e.Name,
        _ => null,
    };

    /// <summary>A short web-safe key per cluster, from its name, unique on the map.</summary>
    static Dictionary<Cluster, string> Keys(IReadOnlyList<Cluster> clusters)
    {
        var keys = new Dictionary<Cluster, string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in clusters)
        {
            var slug = Regex.Replace(c.Name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug.Length == 0) slug = "cluster";
            var key = slug;
            for (var k = 2; !used.Add(key); k++) key = $"{slug}-{k}";
            keys[c] = key;
        }
        return keys;
    }

}
