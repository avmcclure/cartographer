namespace Cartographer;

// The site's sheets (spec section 8): what is on each plan, before and after it is laid out.
// Every position and size is a whole number of grid steps; the site decides how many pixels a step is.

public sealed class Sheet
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>organisation | system | area</summary>
    public string Level { get; set; } = "";
    /// <summary>The entry or group the sheet is about: its selection key.</summary>
    public string? Subject { get; set; }
    /// <summary>The sheet one level up.</summary>
    public string? Up { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>Steps per grid-reference square.</summary>
    public int Cell { get; set; } = Sheets.Cell;
    /// <summary>True when the plots of a system sheet show their parts inside them.</summary>
    public bool Inside { get; set; }
    /// <summary>Set when lines were left off because the sheet is too large to route.</summary>
    public string? Note { get; set; }
    /// <summary>Plots that could not be placed without a re-survey; they are missing from the drawing.</summary>
    public List<string>? Unplaced { get; set; }
    public List<Plot> Plots { get; set; } = [];
    public List<Line> Lines { get; set; } = [];
    /// <summary>The links schedule: every link the sheet covers, drawn or not.</summary>
    public List<ScheduleLink> Links { get; set; } = [];
    /// <summary>Part ID to the routes of its own links, drawn only while it is selected.</summary>
    public SortedDictionary<string, List<PartRoute>> Routes { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Entry ID to the plots that stand for it on this sheet, most specific first.</summary>
    public SortedDictionary<string, List<string>> Where { get; set; } = new(StringComparer.Ordinal);
}

public sealed class Plot
{
    public string Id { get; set; } = "";
    public string? Entry { get; set; }
    public string Number { get; set; } = "";
    public string Ref { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>An entry kind, or system | unassigned.</summary>
    public string Kind { get; set; } = "";
    public string? Type { get; set; }
    /// <summary>The second line written on the plot.</summary>
    public string Sub { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Parent { get; set; }
    public bool Container { get; set; }
    /// <summary>True for a plot that belongs to another sheet and is shown here because something links to it.</summary>
    public bool Neighbour { get; set; }
    public bool Provisional { get; set; }
    public bool Retired { get; set; }
    public bool Drift { get; set; }
    /// <summary>Open questions on the entries the plot stands for.</summary>
    public int Unsurveyed { get; set; }
    public int Parts { get; set; }
    /// <summary>Links leaving the plot, and links arriving at it.</summary>
    public int Uses { get; set; }
    public int UsedBy { get; set; }
    /// <summary>The sheet one level down, when there is one.</summary>
    public string? Opens { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}

public sealed class Line
{
    public string Id { get; set; } = "";
    public string? From { get; set; }
    public string? To { get; set; }
    /// <summary>For a line that leaves the sheet: the group at its far end and that group's sheet.</summary>
    public string? Beyond { get; set; }
    public string? BeyondSheet { get; set; }
    public string Text { get; set; } = "";
    /// <summary>The weakest basis among the links: inferred, else extracted, else stated.</summary>
    public string Basis { get; set; } = "";
    /// <summary>True when at least one of the links was stated by a person.</summary>
    public bool Stated { get; set; }
    public int Total { get; set; }
    public List<VerbCount> Verbs { get; set; } = [];
    /// <summary>The links the line stands for, as [from entry, to entry].</summary>
    public List<string[]> Pairs { get; set; } = [];
    public List<int[]> Points { get; set; } = [];
    public int[]? Label { get; set; }
    /// <summary>The text as the rows it is lettered in on the plan.</summary>
    public string[] Rows => Sheets.Wrap(Text);
    internal bool In;
    internal int LabelWidth => Rows.Max(Sheets.LabelWidth) + (Stated ? 2 : 0);
    internal int LabelHeight => (13 * Rows.Length + 16) / 10;
}

public sealed record VerbCount(string Verb, int Count);

public sealed class ScheduleLink
{
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string Verb { get; set; } = "";
    public string To { get; set; } = "";
    public string ToName { get; set; } = "";
    public string Basis { get; set; } = "";
    public EvidenceLink? Evidence { get; set; }
    /// <summary>The drawn line that carries this link, when one does.</summary>
    public string? Line { get; set; }
}

public sealed class PartRoute
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Text { get; set; } = "";
    public string Basis { get; set; } = "";
    public List<int[]> Points { get; set; } = [];
    public int[]? Label { get; set; }
}

public sealed record EvidenceLink(string Label, string? Url);

/// <summary>A system, a provisional system, or the unassigned plot.</summary>
public sealed record Group(string Key, string Name, string? Entry, bool Provisional)
{
    public string Sheet => "system/" + Key[(Key.LastIndexOf('/') + 1)..];
    /// <summary>The components and databases in the group.</summary>
    public SortedSet<string> Members { get; } = new(StringComparer.Ordinal);
}

/// <summary>A resolved link between two entries that both exist.</summary>
public sealed record Edge(string From, string To, string Type, string Basis, EvidenceLink? Evidence);

/// <summary>The atlas as the plans see it: links, groups and what stands where.</summary>
public sealed class World
{
    public const string Unassigned = "unassigned";
    public Atlas Atlas { get; }
    public List<Edge> Edges { get; } = [];
    public List<Group> Groups { get; } = [];
    readonly Dictionary<string, List<string>> _groupsOf = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> _questions = new(StringComparer.Ordinal);

    public World(Atlas atlas)
    {
        Atlas = atlas;
        foreach (var e in atlas.Entries.Values)
        {
            foreach (var l in e.Links ?? [])
                if (l.To is not null && l.To != e.Id && atlas.Find(l.To) is not null)
                    Edges.Add(new(e.Id, l.To, l.Type, l.Basis, Evidences.Of(atlas, e, l.Source)));
            var open = QuestionList.Parse(e.Body).Items.Count(q => q.State is "open" or "needs-person");
            if (open > 0) _questions[e.Id] = open;
        }

        // Until any system exists, repositories are grouped by the folder-name prefix before the first hyphen.
        var systems = atlas.OfKind(Kinds.System).ToList();
        var groups = new SortedDictionary<string, Group>(StringComparer.Ordinal);
        Group Get(string key, Func<Group> make) => groups.TryGetValue(key, out var g) ? g : groups[key] = make();
        foreach (var s in systems) Get(s.Id, () => new(s.Id, s.Name, s.Id, false));
        foreach (var e in atlas.OfKind(Kinds.Component, Kinds.Database))
        {
            var keys = new List<string>();
            if (systems.Count > 0) keys.AddRange((e.Systems ?? []).Select(s => s.Id).Where(groups.ContainsKey).Distinct());
            else if (e.Kind == Kinds.Component)
            {
                var prefix = e.Id.Split('/')[1].Split('-')[0];
                keys.Add(Get("provisional/" + prefix, () => new("provisional/" + prefix, prefix, null, true)).Key);
            }
            // ponytail: a real system called "unassigned" would share this plot's sheet name; rename it if that ever happens.
            if (keys.Count == 0) keys.Add(Get(Unassigned, () => new(Unassigned, "Unassigned", null, false)).Key);
            foreach (var k in keys) groups[k].Members.Add(e.Id);
            _groupsOf[e.Id] = keys;
        }
        Groups.AddRange(groups.Values.Where(g => g.Key != Unassigned));
        if (groups.TryGetValue(Unassigned, out var unassigned)) Groups.Add(unassigned);

        foreach (var r in atlas.OfKind(Kinds.Repository))
        {
            var area = Ids.Area(r.Id)!;
            var keys = atlas.OfKind(Kinds.Component).Where(c => c.Id.StartsWith(area + "/")).SelectMany(c => _groupsOf[c.Id]).Distinct().Order(StringComparer.Ordinal).ToList();
            if (keys.Count > 0) _groupsOf[r.Id] = keys;
        }
    }

    public Entry Entry(string id) => Atlas.Entries[id];

    /// <summary>The repository or database entry an ID sits under; itself for those two kinds; null for anything else.</summary>
    public string? AreaEntry(string id) => Ids.Area(id) is { } a ? a + (a.StartsWith("repositories/") ? "/repository" : "/database") : null;

    /// <summary>The groups an entry is drawn in. Empty for channels, external systems, systems and flows.</summary>
    public IReadOnlyList<string> GroupsOf(string id)
    {
        if (_groupsOf.TryGetValue(id, out var keys)) return keys;
        var area = AreaEntry(id);
        return area is not null && area != id && _groupsOf.TryGetValue(area, out keys) ? keys : [];
    }

    public Group GroupByKey(string key) => Groups.First(g => g.Key == key);

    /// <summary>Entries under a repository or database, the area entry left out.</summary>
    public IEnumerable<Entry> PartsOf(string areaEntry)
    {
        var prefix = Ids.Area(areaEntry) + "/";
        return Atlas.Entries.Values.Where(e => e.Id.StartsWith(prefix, StringComparison.Ordinal) && e.Id != areaEntry);
    }

    /// <summary>Every entry a group stands for: its members, everything inside member databases, and its repositories.</summary>
    public SortedSet<string> Covered(Group g)
    {
        var all = new SortedSet<string>(g.Members, StringComparer.Ordinal);
        foreach (var m in g.Members)
        {
            if (Entry(m).Kind == Kinds.Database) all.UnionWith(PartsOf(m).Select(p => p.Id));
            else all.Add(AreaEntry(m)!);
        }
        return all;
    }

    public int Questions(string id) => _questions.GetValueOrDefault(id);
}

public static class Sheets
{
    public const int Cell = 20, LeafHeight = 5, TitleHeight = 3, MaxInside = 30, MaxNeighbours = 12;
    public const string Organisation = "organisation";

    public static string AreaSheet(string areaEntry) => "area/" + Ids.Area(areaEntry);

    // Sizes are guesses at the lettering the site draws, at 10 px a step: about 7.3 px a character for a name (after room
    // for a plot number), 6 for the line under it, 5.5 for a line's label, and 26 px for each sign.
    public static int LeafWidth(string name, string sub, int signs) => Math.Clamp((int)Math.Ceiling((Math.Max((name.Length + 5) * 7.3 + signs * 26, sub.Length * 6.0) + 22) / 10), 12, 40);
    public static int LabelWidth(string text) => (int)Math.Ceiling((text.Length * 5.5 + 12) / 10);

    /// <summary>A label in rows of about 20 characters, broken only between its counts, so that it fits between two plots.</summary>
    public static string[] Wrap(string text)
    {
        var rows = new List<string>();
        foreach (var piece in text.Split(" · ").SelectMany((part, i) => i == 0 && text.Contains(" · ") ? [part + " ·"] : part.Split(", ")))
        {
            if (rows.Count > 0 && !rows[^1].EndsWith('·') && rows[^1].Length + piece.Length + 2 <= 20) rows[^1] += ", " + piece;
            else rows.Add(piece);
        }
        for (var i = 0; i < rows.Count - 1; i++) if (!rows[i].EndsWith('·')) rows[i] += ",";
        return [.. rows];
    }

    public static string Verb(string type) => type.Replace('-', ' ');

    /// <summary>Every sheet of the atlas, not yet laid out, in ID order.</summary>
    public static List<Sheet> All(World w)
    {
        var sheets = new List<Sheet> { Organisation_(w) };
        sheets.AddRange(w.Groups.Select(g => System(w, g)));
        sheets.AddRange(w.Atlas.OfKind(Kinds.Repository, Kinds.Database).Select(a => Area(w, a)));
        return [.. sheets.OrderBy(s => s.Id, StringComparer.Ordinal)];
    }

    static Sheet Organisation_(World w)
    {
        var sheet = new Sheet { Id = Organisation, Title = w.Atlas.Config.Organisation, Level = "organisation" };
        foreach (var g in w.Groups)
        {
            var covered = w.Covered(g);
            var databases = g.Members.Count(m => w.Entry(m).Kind == Kinds.Database);
            var parts = g.Members.Count;
            var plot = new Plot
            {
                Id = g.Key, Entry = g.Entry, Name = g.Name, Kind = g.Key == World.Unassigned ? "unassigned" : Kinds.System, Provisional = g.Provisional,
                Summary = g.Entry is null ? (g.Key == World.Unassigned ? "Parts not yet placed in a system." : "Provisional system: repositories whose folder names start with this prefix.") : w.Entry(g.Entry).Summary,
                Sub = $"{Count(parts, "part")}{(databases > 0 ? $" · {Count(databases, "database")}" : "")}", Parts = parts, Opens = g.Sheet,
            };
            Add(sheet, plot);
            foreach (var id in covered) Where(sheet, id, plot.Id);
            if (g.Entry is not null) Where(sheet, g.Entry, plot.Id);
        }
        foreach (var e in w.Atlas.OfKind(Kinds.Channel, Kinds.External)) AddEntryPlot(w, sheet, e, null, false);

        List<string> Homes(string id) => w.Entry(id).Kind is Kinds.Channel or Kinds.External ? [id] : [.. w.GroupsOf(id)];
        var crossing = new SortedDictionary<(string, string), List<Edge>>();
        foreach (var e in w.Edges)
        {
            var (a, b) = (Homes(e.From), Homes(e.To));
            if (a.Intersect(b).Any()) continue;
            foreach (var x in a) foreach (var y in b) Bucket(crossing, (x, y)).Add(e);
        }
        foreach (var ((a, b), edges) in crossing)
            sheet.Lines.Add(Summarise(new Line { Id = $"{a}>{b}", From = a, To = b }, edges, totalOnly: true));
        Finish(w, sheet);
        return sheet;
    }

    static Sheet System(World w, Group g)
    {
        var sheet = new Sheet { Id = g.Sheet, Title = g.Name, Level = "system", Subject = g.Key, Up = Organisation };
        var covered = w.Covered(g);
        var parts = covered.Where(id => w.Entry(id).Kind is not (Kinds.Repository or Kinds.Database)).ToList();
        sheet.Inside = parts.Count <= MaxInside;

        foreach (var id in covered.Where(id => w.Entry(id).Kind is Kinds.Repository or Kinds.Database))
        {
            var inside = parts.Where(p => w.AreaEntry(p) == id).ToList();
            var top = AddEntryPlot(w, sheet, w.Entry(id), null, false);
            top.Container = sheet.Inside && inside.Count > 0;
            top.Parts = inside.Count;
            foreach (var p in inside)
            {
                if (sheet.Inside) AddEntryPlot(w, sheet, w.Entry(p), id, false);
                else Where(sheet, p, id);
            }
        }

        string? Top(string id) => !covered.Contains(id) ? null : w.Entry(id).Kind is Kinds.Repository or Kinds.Database ? id : w.AreaEntry(id);
        var between = new SortedDictionary<(string, string), List<Edge>>();
        var beyond = new SortedDictionary<(string Plot, bool In, string Group), List<Edge>>();
        foreach (var e in w.Edges)
        {
            var (a, b) = (Top(e.From), Top(e.To));
            if (a is null && b is null) continue;
            var link = Scheduled(w, sheet, e);
            if (a is not null && b is null && w.Entry(e.To).Kind is Kinds.Channel or Kinds.External)
            {
                if (sheet.Plots.All(p => p.Id != e.To)) AddEntryPlot(w, sheet, w.Entry(e.To), null, true);
                b = e.To;
            }
            if (a is not null && b is not null)
            {
                if (a != b) { Bucket(between, (a, b)).Add(e); link.Line = $"{a}>{b}"; }
                continue;
            }
            var other = w.GroupsOf(a is null ? e.From : e.To).FirstOrDefault() ?? World.Unassigned;
            var key = (a ?? b!, a is null, other);
            Bucket(beyond, key).Add(e);
            link.Line = EdgeId(key.Item1, key.Item2, other);
        }
        foreach (var ((a, b), edges) in between) sheet.Lines.Add(Summarise(new Line { Id = $"{a}>{b}", From = a, To = b }, edges));
        foreach (var ((plot, incoming, other), edges) in beyond)
        {
            var group = w.Groups.FirstOrDefault(x => x.Key == other);
            var line = new Line { Id = EdgeId(plot, incoming, other), From = incoming ? null : plot, To = incoming ? plot : null, In = incoming, Beyond = group?.Name ?? "Unassigned", BeyondSheet = group?.Sheet };
            Summarise(line, edges);
            line.Text = $"{(incoming ? "from" : "to")} {line.Beyond} · {line.Text}";
            sheet.Lines.Add(line);
        }
        Finish(w, sheet);
        return sheet;
    }

    static Sheet Area(World w, Entry area)
    {
        var groups = w.GroupsOf(area.Id);
        var sheet = new Sheet { Id = AreaSheet(area.Id), Title = area.Name, Level = "area", Subject = area.Id, Up = groups.Count > 0 ? w.GroupByKey(groups[0]).Sheet : Organisation };
        var parts = w.PartsOf(area.Id).ToList();
        var inArea = parts.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var p in parts) AddEntryPlot(w, sheet, p, null, false);
        // ponytail: an area of more than 30 parts is drawn as plots only, its links listed but not routed.
        // Route them (or only the selection's) when Groma's run time on big sheets is known to allow it.
        var draw = parts.Count <= MaxInside;
        if (!draw) sheet.Note = $"This area has {parts.Count} parts. Its links are listed in the links schedule and are not drawn.";

        string Neighbour(string id) => w.Entry(id).Kind is Kinds.Channel or Kinds.External ? id : w.AreaEntry(id) ?? id;
        var touching = w.Edges.Where(e => (inArea.Contains(e.From) || inArea.Contains(e.To) || e.From == area.Id || e.To == area.Id)).ToList();
        var outside = touching.Where(e => inArea.Contains(e.From) != inArea.Contains(e.To) && e.From != area.Id && e.To != area.Id)
            .Select(e => Neighbour(inArea.Contains(e.From) ? e.To : e.From)).Distinct().Order(StringComparer.Ordinal).ToList();
        var asPlots = draw && outside.Count <= MaxNeighbours;
        if (asPlots)
            foreach (var n in outside)
            {
                AddEntryPlot(w, sheet, w.Entry(n), null, true);
                if (w.Entry(n).Kind is Kinds.Repository or Kinds.Database) foreach (var p in w.PartsOf(n)) Where(sheet, p.Id, n);
            }

        var lines = new SortedDictionary<(string From, string To), List<Edge>>();
        var edges = new SortedDictionary<(string Plot, bool In, string Group), List<Edge>>();
        foreach (var e in touching)
        {
            var link = Scheduled(w, sheet, e);
            var (a, b) = (inArea.Contains(e.From), inArea.Contains(e.To));
            if (!draw || (!a && !b) || (a ? e.To : e.From) == area.Id) continue;
            if (a && b || asPlots)
            {
                var key = (a ? e.From : Neighbour(e.From), b ? e.To : Neighbour(e.To));
                Bucket(lines, key).Add(e);
                link.Line = $"{key.Item1}>{key.Item2}";
            }
            else
            {
                var key = (a ? e.From : e.To, !a, "elsewhere");
                Bucket(edges, key).Add(e);
                link.Line = EdgeId(key.Item1, key.Item2, key.Item3);
            }
        }
        foreach (var ((a, b), list) in lines) sheet.Lines.Add(Summarise(new Line { Id = $"{a}>{b}", From = a, To = b }, list));
        foreach (var ((plot, incoming, other), list) in edges)
        {
            var line = Summarise(new Line { Id = EdgeId(plot, incoming, other), From = incoming ? null : plot, To = incoming ? plot : null, In = incoming, Beyond = "other areas" }, list);
            line.Text = $"{(incoming ? "from" : "to")} other areas · {line.Text}";
            sheet.Lines.Add(line);
        }
        Finish(w, sheet);
        return sheet;
    }

    // ---- shared ----

    static string EdgeId(string plot, bool incoming, string other) => incoming ? $"edge:{other}>{plot}" : $"{plot}>edge:{other}";

    static List<T> Bucket<K, T>(SortedDictionary<K, List<T>> map, K key) where K : notnull => map.TryGetValue(key, out var list) ? list : map[key] = [];

    static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

    static void Add(Sheet sheet, Plot plot)
    {
        plot.H = plot.Container ? TitleHeight : LeafHeight;
        sheet.Plots.Add(plot);
        Where(sheet, plot.Entry ?? plot.Id, plot.Id);
    }

    static void Where(Sheet sheet, string entry, string plot)
    {
        if (!sheet.Where.TryGetValue(entry, out var list)) sheet.Where[entry] = list = [];
        if (!list.Contains(plot)) list.Add(plot);
    }

    static Plot AddEntryPlot(World w, Sheet sheet, Entry e, string? parent, bool neighbour)
    {
        var area = e.Kind is Kinds.Repository or Kinds.Database;
        var parts = area ? w.PartsOf(e.Id).Count() : 0;
        var plot = new Plot
        {
            Id = e.Id, Entry = e.Id, Name = e.Name, Kind = e.Kind, Type = e.Type, Summary = e.Summary, Parent = parent, Neighbour = neighbour,
            Retired = e.Status == "retired", Parts = parts, Opens = area ? AreaSheet(e.Id) : null,
            Sub = e.Kind switch
            {
                Kinds.Repository => $"repository · {Count(parts, "component")}",
                Kinds.Database => $"database · {Count(parts, "part")}",
                Kinds.Component => $"{e.Type}{(e.Stack is { Count: > 0 } s ? " · " + s[0] : "")}",
                Kinds.External => "external system",
                _ => e.Type ?? e.Kind,
            },
        };
        Add(sheet, plot);
        return plot;
    }

    static ScheduleLink Scheduled(World w, Sheet sheet, Edge e)
    {
        var link = new ScheduleLink { From = e.From, FromName = w.Entry(e.From).Name, Verb = Verb(e.Type), To = e.To, ToName = w.Entry(e.To).Name, Basis = e.Basis, Evidence = e.Evidence };
        sheet.Links.Add(link);
        return link;
    }

    static Line Summarise(Line line, List<Edge> edges, bool totalOnly = false)
    {
        line.Total = edges.Count;
        line.Verbs = [.. edges.GroupBy(e => Verb(e.Type)).Select(g => new VerbCount(g.Key, g.Count())).OrderByDescending(v => v.Count).ThenBy(v => v.Verb, StringComparer.Ordinal)];
        line.Text = totalOnly ? edges.Count.ToString() : string.Join(", ", line.Verbs.Select(v => $"{v.Verb} {v.Count}"));
        line.Basis = edges.Any(e => e.Basis == Basis.Inferred) ? Basis.Inferred : edges.Any(e => e.Basis == Basis.Extracted) ? Basis.Extracted : Basis.Stated;
        line.Stated = edges.Any(e => e.Basis == Basis.Stated);
        line.Pairs = [.. edges.Select(e => (e.From, e.To)).Distinct().OrderBy(p => p).Select(p => new[] { p.From, p.To })];
        return line;
    }

    /// <summary>Sizes, counts and signs that depend on everything being on the sheet.</summary>
    static void Finish(World w, Sheet sheet)
    {
        var byId = sheet.Plots.ToDictionary(p => p.Id, StringComparer.Ordinal);
        HashSet<string> Standing(string entry)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in sheet.Where.GetValueOrDefault(entry) ?? [])
                for (var p = byId[id]; p is not null; p = p.Parent is null ? null : byId[p.Parent]) set.Add(p.Id);
            return set;
        }
        foreach (var e in w.Edges)
        {
            var (a, b) = (Standing(e.From), Standing(e.To));
            foreach (var id in a.Except(b)) byId[id].Uses++;
            foreach (var id in b.Except(a)) byId[id].UsedBy++;
        }
        foreach (var (entry, _) in sheet.Where)
        {
            if (w.Atlas.Find(entry) is not { } e) continue;
            foreach (var id in Standing(entry))
            {
                byId[id].Unsurveyed += w.Questions(entry);
                byId[id].Drift |= e.Drift is { Count: > 0 };
            }
        }
        foreach (var p in sheet.Plots)
        {
            p.W = LeafWidth(p.Name, p.Container ? "" : p.Sub, (p.Drift ? 1 : 0) + (p.Unsurveyed > 0 ? 1 : 0));
            p.H = p.Container ? TitleHeight : LeafHeight;
        }
        sheet.Plots.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        sheet.Lines.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        sheet.Links = [.. sheet.Links.OrderBy(l => l.From, StringComparer.Ordinal).ThenBy(l => l.To, StringComparer.Ordinal).ThenBy(l => l.Verb, StringComparer.Ordinal)];
    }
}

/// <summary>Evidence pointers (spec section 3) as text a person can read and, where a location is known, a link.</summary>
public static class Evidences
{
    public static EvidenceLink? Of(Atlas atlas, Entry entry, string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var hash = source.LastIndexOf("#L", StringComparison.Ordinal);
        var (key, lines) = hash < 0 ? (source, "") : (source[..hash], source[(hash + 1)..]);
        var pointer = entry.Sources?.GetValueOrDefault(key) ?? key;
        var at = pointer.LastIndexOf('@');
        var colon = pointer.IndexOf(':');
        if (at < 0 || colon < 0 || colon > at) return new(source, null);
        var (head, rest, tail) = (pointer[..colon], pointer[(colon + 1)..at], pointer[(at + 1)..]);
        if (head == "person") return new($"stated by {rest}, {tail}", null);
        if (head == "instance") return new($"live catalogue, {rest}, {tail}", null);

        var range = lines.Length == 0 ? [] : lines.Split('-').Select(l => l.TrimStart('L')).ToArray();
        var where = range.Length == 0 ? "" : range.Length == 1 ? $", line {range[0]}" : $", lines {range[0]}-{range[1]}";
        var label = $"{rest[(rest.LastIndexOf('/') + 1)..]} at {tail}{where}";
        var locations = atlas.Find(Ids.Repository(head))?.Locations;
        var location = (locations?.FirstOrDefault(l => l.Primary) ?? locations?.FirstOrDefault())?.Id;
        return new(label, location is null ? null : Url(location, rest, tail, range));
    }

    /// <summary>The page of a file at a commit on GitHub or Azure DevOps.</summary>
    public static string? Url(string location, string path, string commit, string[] lines)
    {
        var p = location[(location.IndexOf(':') + 1)..].Split('/', 3);
        var file = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        if (location.StartsWith("github:") && p.Length == 2)
            return $"https://github.com/{p[0]}/{p[1]}/blob/{commit}/{file}" + (lines.Length == 0 ? "" : "#L" + string.Join("-L", lines));
        if (location.StartsWith("azdo:") && p.Length == 3)
            return $"https://dev.azure.com/{p[0]}/{Uri.EscapeDataString(p[1])}/_git/{Uri.EscapeDataString(p[2])}?path=/{file}&version=GC{commit}" +
                (lines.Length == 0 ? "" : $"&line={lines[0]}&lineEnd={lines[^1]}&lineStartColumn=1&lineEndColumn=200");
        return null;
    }

    /// <summary>The web address of a repository location.</summary>
    public static string? Home(string location)
    {
        var p = location[(location.IndexOf(':') + 1)..].Split('/', 3);
        return location.StartsWith("github:") && p.Length == 2 ? $"https://github.com/{p[0]}/{p[1]}"
            : location.StartsWith("azdo:") && p.Length == 3 ? $"https://dev.azure.com/{p[0]}/{Uri.EscapeDataString(p[1])}/_git/{Uri.EscapeDataString(p[2])}" : null;
    }
}
