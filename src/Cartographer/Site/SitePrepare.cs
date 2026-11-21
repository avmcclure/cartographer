using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>
/// Writes everything the site is built from (spec section 8) as JSON files, and the plan layouts into the atlas.
/// The shapes are described in site/README.md. Output depends only on the atlas: sorted, no timestamps.
/// </summary>
public static class SitePrepare
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, NewLine = "\n", PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The names of what prepare writes directly under the output directory; anything else there is left alone.</summary>
    static readonly string[] Owned = ["entries", "sheets", "site.json", "gazetteer.json", "graph.json", "questions.json", "changes.json", "tags.json"];

    /// <param name="editUrl">Where an atlas file can be edited, e.g. https://github.com/acme/atlas/edit/main; null for no edit links.</param>
    public static List<Surveyed> Run(Atlas atlas, string outDir, string? editUrl = null)
    {
        var w = new World(atlas);
        var sheets = Sheets.All(w);
        var surveyed = sheets.Select(s => Surveyor.Lay(w, s)).ToList();

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

        foreach (var s in sheets) Write($"sheets/{s.Id}.json", s);

        // Where each entry is drawn as a plot of its own: the most detailed sheet that has it.
        var home = new Dictionary<string, (string Sheet, Plot Plot)>(StringComparer.Ordinal);
        foreach (var level in new[] { "area", "system", "organisation" })
            foreach (var s in sheets.Where(s => s.Level == level))
                foreach (var p in s.Plots.Where(p => p.Entry is not null && !p.Neighbour)) home.TryAdd(p.Entry!, (s.Id, p));
        string? Opens(Entry e) => e.Kind switch
        {
            Kinds.Repository or Kinds.Database => Sheets.AreaSheet(e.Id),
            Kinds.System => w.Groups.FirstOrDefault(g => g.Entry == e.Id)?.Sheet,
            _ => null,
        };

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
            var at = home.TryGetValue(e.Id, out var h) ? new { h.Sheet, plot = h.Plot.Id, h.Plot.Number, h.Plot.Ref } : null;
            Write($"entries/{e.Id}.json", new
            {
                e.Id, e.Kind, e.Type, e.Name, e.Summary, e.Status,
                repository = Ref(e.Repository), database = Ref(e.Database), e.Engine, e.Stack, e.Aliases, e.Instances, e.Packages,
                locations = e.Locations?.Select(l => new { l.Id, l.Primary, url = Evidences.Home(l.Id) }),
                provides = e.Provides?.Select(p => new { p.Name, p.As, p.Basis, evidence = Ev(p.Source) }),
                systems = e.Systems?.Select(s => new { system = Ref(s.Id), s.Basis, evidence = Ev(s.Source) }),
                tags = e.Tags?.Select(t => new { t.Name, t.Basis, evidence = Ev(t.Source) }),
                owner = e.Owner is { } o ? new { o.Name, o.Basis, evidence = Ev(o.Source) } : null,
                uses = e.Links?.Select(l => new { l.Type, verb = Sheets.Verb(l.Type), to = Ref(l.To), unresolved = l.To is null ? l.Name : null, l.Name, l.Database, l.Version, l.How, via = Ref(l.Via), names = l.Uses, l.Count, l.Basis, evidence = Ev(l.Source) }),
                usedBy,
                drift = e.Drift?.Select(d => new { d.What, sources = d.Sources.Select(Ev) }),
                e.Verified, stale = Stale(e) ? true : (bool?)null,
                sections = Sections.Split(e.Body).Where(s => s.Title != Sections.Questions && s.Text.Trim().Length > 0).Select(s => new { s.Title, markdown = s.Text }),
                questions = questions[e.Id].Items.Select(q => new { q.Id, q.State, q.RaisedBy, q.Attempts, q.Missing, q.WaitingOn, q.AnswerLooksLike, q.Answer }),
                dismissed = questions[e.Id].Dismissed.Count > 0 ? questions[e.Id].Dismissed : null,
                home = at, opens = Opens(e), editUrl = Edit(e.Id),
            });
        }

        Write("gazetteer.json", atlas.Entries.Values.Select(e => new
        {
            e.Id, e.Name, e.Kind, e.Type, e.Summary, e.Status,
            sheet = home.TryGetValue(e.Id, out var h) ? h.Sheet : null, number = h.Plot?.Number, @ref = h.Plot?.Ref,
            unsurveyed = w.Questions(e.Id), verified = e.Verified?.Date, stale = Stale(e) ? true : (bool?)null,
        }));

        // Everything selection needs in the browser: who links to whom, and which entries a group stands for.
        Write("graph.json", new
        {
            entries = new SortedDictionary<string, string[]>(atlas.Entries.Values.ToDictionary(e => e.Id, e => new[] { e.Name, e.Kind }), StringComparer.Ordinal),
            links = w.Edges.Select(e => new[] { e.From, e.To, e.Type, e.Basis }),
            groups = new SortedDictionary<string, IEnumerable<string>>(w.Groups.ToDictionary(g => g.Key, g => (IEnumerable<string>)w.Covered(g)), StringComparer.Ordinal),
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

        // Business-data tags: the tables that carry each, and the system sheets those tables are drawn on.
        Write("tags.json", atlas.OfKind(Kinds.Table, Kinds.View).SelectMany(e => (e.Tags ?? []).Select(t => (Tag: t, Entry: e)))
            .GroupBy(x => x.Tag.Name).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new
            {
                name = g.Key,
                tables = g.OrderBy(x => x.Entry.Id, StringComparer.Ordinal).Select(x => new { entry = Ref(x.Entry.Id), x.Tag.Basis, evidence = Evidences.Of(atlas, x.Entry, x.Tag.Source) }),
                sheets = g.SelectMany(x => w.GroupsOf(x.Entry.Id)).Distinct().Order(StringComparer.Ordinal).Select(k => w.GroupByKey(k).Sheet),
            }));

        Write("site.json", new
        {
            organisation = atlas.Config.Organisation, cartographer = Clock.Version, latestVerified = latest, staleAfterDays = atlas.Config.Limits.VerifiedAgeDays,
            editable = editUrl is not null,
            sheets = sheets.Select(s => new { s.Id, s.Title, s.Level, s.Subject, s.Up, plots = s.Plots.Count, unplaced = s.Unplaced }),
        });
        return surveyed;
    }

    /// <summary>Lays one sheet out afresh and stores the layout. Grid references on it change.</summary>
    public static Surveyed Resurvey(Atlas atlas, string sheetId)
    {
        var w = new World(atlas);
        var sheets = Sheets.All(w);
        var sheet = sheets.FirstOrDefault(s => s.Id == sheetId)
            ?? throw new UsageException($"no sheet '{sheetId}'. Sheets: {string.Join(", ", sheets.Select(s => s.Id))}");
        return Surveyor.Lay(w, sheet, fresh: true);
    }
}
