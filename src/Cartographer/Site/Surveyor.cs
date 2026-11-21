using System.Diagnostics;
using G = Groma;

namespace Cartographer;

/// <summary>What laying out one sheet did.</summary>
/// <param name="Unplaced">Names of plots left off because placing them needs a re-survey; empty when all went well.</param>
public sealed record Surveyed(string Sheet, bool Written, List<string> Unplaced, string? Reason, G.Report Measures, TimeSpan Time);

/// <summary>
/// Lays sheets out with Groma. The layout of a sheet is kept in the atlas at layouts/&lt;sheet&gt;.layout.json, and a plot that
/// is in that file never moves: only a deliberate re-survey lays a sheet out afresh.
/// </summary>
public static class Surveyor
{
    static readonly G.Options Options = new() { BoxGap = 7 };
    const int LabelHeight = 2;

    public static string PathOf(Atlas atlas, string sheet) => Path.Combine(atlas.Root, "layouts", sheet.Replace('/', Path.DirectorySeparatorChar) + ".layout.json");

    /// <summary>Fills in the sheet's positions, routes, numbers and grid references, and stores the layout when it changed.</summary>
    /// <param name="fresh">Ignore the stored layout: a re-survey.</param>
    public static Surveyed Lay(World w, Sheet sheet, bool fresh = false)
    {
        var clock = Stopwatch.StartNew();
        var path = PathOf(w.Atlas, sheet.Id);
        var stored = File.Exists(path) ? File.ReadAllText(path) : null;
        var unplaced = new List<string>();
        string? reason = null;
        G.Layout layout;
        if (sheet.Plots.Count == 0) layout = new G.Layout(Sheets.Cell, Sheets.Cell, [], []);
        else if (stored is null || fresh) layout = G.Plan.Survey(Graph(sheet), Options);
        else
        {
            G.Layout previous;
            try { previous = G.LayoutFile.Read(stored); }
            catch (G.GromaInputException e) { throw new UsageException($"{path} cannot be read ({e.Message}). Lay the sheet out afresh with: cartographer plan resurvey {sheet.Id}"); }
            try { layout = G.Plan.Update(Graph(sheet), previous, Options); }
            catch (G.ResurveyNeededException e)
            {
                reason = e.Message;
                layout = AsStored(sheet, previous, unplaced);
                stored = G.LayoutFile.Write(layout);   // nothing is written: the stored layout stays as it was
            }
        }

        var text = G.LayoutFile.Write(layout);
        var written = reason is null && sheet.Plots.Count > 0 && text != stored;
        if (written)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        Apply(sheet, layout);
        if (sheet.Inside && reason is null) RouteParts(w, sheet, layout);
        sheet.Unplaced = unplaced.Count > 0 || reason is not null ? unplaced : null;
        return new(sheet.Id, written, unplaced, reason, G.Metrics.Measure(layout), clock.Elapsed);
    }

    static G.Graph Graph(Sheet s) => new(
        [.. s.Plots.Select(p => new G.Box(p.Id, p.W, p.H, p.Parent))],
        [.. s.Lines.Select(l => new G.Link(l.Id, l.From, l.To, l.From is null ? G.Side.Left : null, l.To is null ? G.Side.Right : null, l.LabelWidth, l.LabelHeight))]);

    /// <summary>
    /// The sheet as its stored layout can still show it: plots the layout does not hold are taken off and named in
    /// <paramref name="unplaced"/>, and lines are routed again only if that can be done without moving anything.
    /// </summary>
    static G.Layout AsStored(Sheet sheet, G.Layout previous, List<string> unplaced)
    {
        var was = previous.Boxes.ToDictionary(b => b.Id, StringComparer.Ordinal);
        unplaced.AddRange(sheet.Plots.Where(p => !was.ContainsKey(p.Id)).Select(p => p.Name));
        sheet.Plots.RemoveAll(p => !was.ContainsKey(p.Id));
        sheet.Lines.RemoveAll(l => (l.From is not null && !was.ContainsKey(l.From)) || (l.To is not null && !was.ContainsKey(l.To)));
        foreach (var p in sheet.Plots)
        {
            (p.W, p.H) = (was[p.Id].Width, p.Container ? p.H : was[p.Id].Height);
            if (p.Parent is not null && !was.ContainsKey(p.Parent)) p.Parent = null;
        }
        try { return G.Plan.Update(Graph(sheet), previous, Options); }
        catch (G.ResurveyNeededException)
        {
            sheet.Lines.Clear();
            var ids = sheet.Plots.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            return previous with { Boxes = [.. previous.Boxes.Where(b => ids.Contains(b.Id))], Links = [] };
        }
    }

    static void Apply(Sheet sheet, G.Layout layout)
    {
        (sheet.Width, sheet.Height) = (layout.Width, layout.Height);
        var boxes = layout.Boxes.ToDictionary(b => b.Id, StringComparer.Ordinal);
        foreach (var p in sheet.Plots) (p.X, p.Y, p.W, p.H) = (boxes[p.Id].X, boxes[p.Id].Y, boxes[p.Id].Width, boxes[p.Id].Height);
        var routes = layout.Links.ToDictionary(l => l.Id, StringComparer.Ordinal);
        foreach (var l in sheet.Lines) (l.Points, l.Label) = (Points(routes[l.Id]), Label(routes[l.Id]));

        // Numbers run in reading order; a part's number is its plot's, a stroke, and its own. A grid reference names the
        // square a plot's top-left corner is in, and for a plot that holds others, the square of its far corner too.
        static IEnumerable<Plot> Reading(IEnumerable<Plot> plots) => plots.OrderBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.Id, StringComparer.Ordinal);
        var n = 0;
        foreach (var top in Reading(sheet.Plots.Where(p => p.Parent is null)))
        {
            top.Number = (++n).ToString();
            var m = 0;
            foreach (var part in Reading(sheet.Plots.Where(p => p.Parent == top.Id))) part.Number = $"{top.Number}/{++m}";
        }
        foreach (var p in sheet.Plots)
        {
            var (from, to) = (Reference(p.X, p.Y), Reference(p.X + p.W - 1, p.Y + p.H - 1));
            p.Ref = p.Container && from != to ? $"{from}–{to}" : from;
        }
    }

    public static string Reference(int x, int y)
    {
        var (column, letters) = (x / Sheets.Cell, "");
        for (var c = column; c >= 0; c = c / 26 - 1) letters = (char)('A' + c % 26) + letters;
        return $"{letters}{y / Sheets.Cell + 1}";
    }

    static List<int[]> Points(G.Route r) => [.. r.Points.Select(p => new[] { p.X, p.Y })];
    static int[]? Label(G.Route r) => r.Label is { } b ? [b.X, b.Y, b.Width, b.Height] : null;

    /// <summary>
    /// For each part drawn inside a plot, routes for its own links to whatever else is on the sheet. They are drawn only
    /// while the part is selected, so each part's are routed over the standing lines alone, not over each other's.
    /// </summary>
    static void RouteParts(World w, Sheet sheet, G.Layout layout)
    {
        var plots = sheet.Plots.ToDictionary(p => p.Id, StringComparer.Ordinal);
        string? PlotOf(string entry) => sheet.Where.GetValueOrDefault(entry)?.FirstOrDefault();
        foreach (var part in sheet.Plots.Where(p => p.Parent is not null))
        {
            var mine = w.Edges.Where(e => e.From == part.Id || e.To == part.Id)
                .Select(e => (Edge: e, From: PlotOf(e.From), To: PlotOf(e.To)))
                .Where(x => x.From is not null && x.To is not null && x.From != x.To && plots[x.From].Parent != x.To && plots[x.To].Parent != x.From)
                .GroupBy(x => (x.From!, x.To!)).OrderBy(g => g.Key, Ord.Pair).ToList();
            if (mine.Count == 0) continue;
            var links = mine.Select((g, i) =>
            {
                var text = string.Join(", ", g.GroupBy(x => Sheets.Verb(x.Edge.Type)).OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Key));
                var basis = g.Any(x => x.Edge.Basis == Basis.Inferred) ? Basis.Inferred : g.Any(x => x.Edge.Basis == Basis.Extracted) ? Basis.Extracted : Basis.Stated;
                return (Link: new G.Link($"part:{i:000}", g.Key.Item1, g.Key.Item2, null, null, Sheets.LabelWidth(text), LabelHeight), Text: text, Basis: basis);
            }).ToList();
            var routed = G.Plan.RouteExtra(layout, [.. links.Select(l => l.Link)], Options).Links.ToDictionary(l => l.Id, StringComparer.Ordinal);
            sheet.Routes[part.Id] = [.. links.Select(l => new PartRoute { From = l.Link.From!, To = l.Link.To!, Text = l.Text, Basis = l.Basis, Points = Points(routed[l.Link.Id]), Label = Label(routed[l.Link.Id]) })];
        }
    }
}
