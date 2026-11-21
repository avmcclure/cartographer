// Throwaway trial for issue #29: MSAGL layered layout + rectilinear routing on the plan-layout prototype graphs.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Initial;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using Microsoft.Msagl.Routing.Rectilinear;
using static Geo;

var dir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
Directory.CreateDirectory(Path.Combine(dir, "out"));
Console.WriteLine("| Sheet | Step | Boxes | Lines | Moved | Avg | Max | Crossings | Through boxes | On top | Overlapping boxes | Unrouted | Diagonal segs | Labels placed | Labels clashing | Layout ms | Route ms | Label ms |");
Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
var notes = new List<string>();
foreach (var key in new[] { "real", "large" })
{
    var scans = Load(Path.Combine(dir, key + ".json"));
    Dictionary<string, R> prev = null;
    for (int i = 0; i < scans.Count; i++)
    {
        var s = scans[i];
        var sw = Stopwatch.StartNew();
        Dictionary<string, R> rects;
        if (i == 0)
        {
            var raw = FirstLayout(s);
            var pos = new Dictionary<string, R>();
            double minX = raw.Values.Min(r => r.X), minY = raw.Values.Min(r => r.Y);
            foreach (var n in s.Nodes.Where(n => !n.IsC))
            {
                var r = raw[n.Id];
                pos[n.Id] = new R(Snap(r.X - minX) + 40, Snap(r.Y - minY) + 60, n.W, n.H);
            }
            rects = Carry(s, pos);
        }
        else
        {
            notes.Add($"{key} scan {i + 1}: {TryRelayout(s, prev)}");
            sw.Restart();
            rects = Carry(s, prev);
        }
        double layoutMs = sw.Elapsed.TotalMilliseconds;
        var (routes, labels, routeMs, labelMs) = Route(s, rects);
        var m = Metrics(s, rects, routes, labels);
        string mv = "", avg = "", max = "";
        if (i > 0)
        {
            int surv = 0, moved = 0; double sum = 0, mx = 0;
            foreach (var n in s.Nodes.Where(n => !n.IsC && prev.ContainsKey(n.Id) && !scans[i - 1].ById[n.Id].IsC))
            {
                surv++;
                double d = Math.Sqrt(Math.Pow(prev[n.Id].X - rects[n.Id].X, 2) + Math.Pow(prev[n.Id].Y - rects[n.Id].Y, 2)) / 120;
                if (d > 0.004) { moved++; sum += d; mx = Math.Max(mx, d); }
            }
            mv = $"{moved} of {surv}"; avg = (surv > 0 ? sum / surv : 0).ToString("0.00"); max = mx.ToString("0.00");
        }
        Console.WriteLine($"| {key} | {(i == 0 ? "Scan 1" : $"{i} to {i + 1}")} | {s.Nodes.Count} | {s.Edges.Count} | {mv} | {avg} | {max} | {m.cross} | {m.through} | {m.share} | {m.boxes} | {m.unrouted} | {m.diag} | {labels.Count(l => l != null)} | {m.labelClash} | {layoutMs:0} | {routeMs:0} | {labelMs:0} |");
        File.WriteAllText(Path.Combine(dir, "out", $"{key}-scan{i + 1}.svg"), Svg(s, rects, routes, labels, prev));
        prev = rects;
    }
}
Console.WriteLine();
foreach (var n in notes) Console.WriteLine("- " + n);

static List<Scan> Load(string path)
{
    var scans = new List<Scan>();
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var js in doc.RootElement.GetProperty("scans").EnumerateArray())
    {
        var s = new Scan();
        string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        foreach (var jn in js.GetProperty("nodes").EnumerateArray())
            s.Nodes.Add(new N { Id = Str(jn, "id"), Name = Str(jn, "name"), Kind = Str(jn, "kind"), Parent = Str(jn, "parent") });
        foreach (var je in js.GetProperty("edges").EnumerateArray())
            s.Edges.Add(new E(Str(je, "from"), Str(je, "to"), Str(je, "label") ?? ""));
        foreach (var n in s.Nodes)
        {
            n.IsC = s.Nodes.Any(c => c.Parent == n.Id);
            // same box sizes as the JavaScript prototype, minus the second text line it derives (kind text): approximated by the kind name
            n.W = Math.Ceiling((40 + Math.Max(n.Name.Length * 6.7, (n.Kind.Length + 12) * 5.5)) / 20) * 20;
            n.H = 40;
        }
        s.ById = s.Nodes.ToDictionary(n => n.Id);
        scans.Add(s);
    }
    return scans;
}

// Builds an MSAGL graph. With rects: boxes fixed there, containers given as fixed cluster boundaries.
static (GeometryGraph g, Dictionary<string, Node> map, List<Edge> edges) Build(Scan s, Dictionary<string, R> rects)
{
    var g = new GeometryGraph();
    g.RootCluster.UserData = "root"; // CalculateLayout looks clusters up by UserData and throws on null
    var map= new Dictionary<string, Node>();
    foreach (var n in s.Nodes.Where(n => n.IsC))
    {
        var c = new Cluster { UserData = n.Id };
        if (rects != null) { var r = rects[n.Id]; c.RectangularBoundary = new RectangularClusterBoundary(); c.BoundaryCurve = CurveFactory.CreateRectangle(r.W, r.H, new Point(r.X + r.W / 2, r.Y + r.H / 2)); }
        else c.RectangularBoundary = new RectangularClusterBoundary { LeftMargin = 20, RightMargin = 20, TopMargin = 40, BottomMargin = 40 };
        map[n.Id] = c;
        g.RootCluster.AddChild(c);
    }
    foreach (var n in s.Nodes.Where(n => !n.IsC))
    {
        var r = rects != null && rects.TryGetValue(n.Id, out var rr) ? rr : new R(0, 0, n.W, n.H);
        var node = new Node(CurveFactory.CreateRectangle(n.W, n.H, new Point(r.X + r.W / 2, r.Y + r.H / 2)), n.Id);
        map[n.Id] = node;
        g.Nodes.Add(node);
        if (n.Parent != null) ((Cluster)map[n.Parent]).AddChild(node); else g.RootCluster.AddChild(node);
    }
    var edges = new List<Edge>();
    foreach (var e in s.Edges)
    {
        var edge = new Edge(map[e.From], map[e.To]);
        edge.Label = new Label(Math.Ceiling(e.Label.Length * 5.4 + 8), 12, edge);
        g.Edges.Add(edge);
        edges.Add(edge);
    }
    return (g, map, edges);
}

static Dictionary<string, R> FirstLayout(Scan s)
{
    var (g, map, _) = Build(s, null);
    var settings = new SugiyamaLayoutSettings
    {
        Transformation = PlaneTransformation.Rotation(Math.PI / 2), // layers left to right, like the prototype
        NodeSeparation = 40, LayerSeparation = 80, ClusterMargin = 40,
        EdgeRoutingSettings = { EdgeRoutingMode = EdgeRoutingMode.StraightLine }, // lines are re-routed afterwards on the snapped boxes
    };
    LayoutHelpers.CalculateLayout(g, settings, null);
    return map.ToDictionary(k => k.Key, k => ToR(k.Value.BoundingBox));
}

// MSAGL's own incremental facility: does it place only the new boxes? Result is reported, not used.
static string TryRelayout(Scan s, Dictionary<string, R> prev)
{
    try
    {
        var known = s.Nodes.Where(n => !n.IsC && prev.ContainsKey(n.Id)).ToList();
        var start = known.ToDictionary(n => n.Id, n => prev[n.Id]);
        foreach (var n in s.Nodes.Where(n => n.IsC)) { var c = ContOf(s, n, start, null); if (c != null) start[n.Id] = c; }
        // a container with no surviving child cannot be given a boundary: skip the trial rather than invent one
        if (s.Nodes.Any(n => n.IsC && !start.ContainsKey(n.Id))) return "Relayout not tried (a new container has no placed child)";
        var (g, map, _) = Build(s, start);
        var added = s.Nodes.Where(n => !n.IsC && !prev.ContainsKey(n.Id)).Select(n => map[n.Id]).ToList();
        var sw = Stopwatch.StartNew();
        new Relayout(g, new Node[0], added, c => new SugiyamaLayoutSettings()).Run();
        // displacement measured relative to the first surviving box, so a uniform shift of the whole sheet does not count as movement
        var r0 = ToR(map[known[0].Id].BoundingBox); double ox = r0.X - prev[known[0].Id].X, oy = r0.Y - prev[known[0].Id].Y;
        int moved = known.Count(n => { var r = ToR(map[n.Id].BoundingBox); return Math.Abs(r.X - ox - prev[n.Id].X) > 0.5 || Math.Abs(r.Y - oy - prev[n.Id].Y) > 0.5; });
        var leaves = s.Nodes.Where(n => !n.IsC).Select(n => ToR(map[n.Id].BoundingBox)).ToList();
        int overlaps = 0;
        for (int a = 0; a < leaves.Count; a++) for (int b = a + 1; b < leaves.Count; b++) if (Hit(leaves[a], leaves[b], -1)) overlaps++;
        return $"MSAGL Relayout with {added.Count} added boxes ran in {sw.Elapsed.TotalMilliseconds:0} ms, moved {moved} of {known.Count} surviving boxes, left {overlaps} overlapping box pairs";
    }
    catch (Exception ex) { return $"MSAGL Relayout threw {ex.GetType().Name}: {ex.Message} {ex.StackTrace.Split((char)10)[0].Trim()}"; }
}

// Kept positions (own code, ported from the JavaScript prototype): survivors stay, new boxes go to the nearest free grid slot near their neighbours.
static Dictionary<string, R> Carry(Scan s, Dictionary<string, R> prev)
{
    var rects = new Dictionary<string, R>();
    foreach (var n in s.Nodes) if (!n.IsC && prev.TryGetValue(n.Id, out var p)) rects[n.Id] = new R(p.X, p.Y, n.W, n.H);
    void FixC() { foreach (var n in s.Nodes) if (n.IsC) { var c = ContOf(s, n, rects, null); if (c != null) rects[n.Id] = c; } }
    FixC();
    foreach (var n in s.Nodes) if (!n.IsC && !rects.ContainsKey(n.Id)) { rects[n.Id] = Place(s, n, rects); FixC(); }
    return rects;
}

static R ContOf(Scan s, N n, Dictionary<string, R> rects, R extra)
{
    double x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
    void Inc(R r) { x0 = Math.Min(x0, r.X); y0 = Math.Min(y0, r.Y); x1 = Math.Max(x1, r.X + r.W); y1 = Math.Max(y1, r.Y + r.H); }
    foreach (var c in s.Nodes) if (c.Parent == n.Id && rects.TryGetValue(c.Id, out var r)) Inc(r);
    if (extra != null) Inc(extra);
    return x0 > 1e8 ? null : new R(x0 - 20, y0 - 40, x1 - x0 + 40, y1 - y0 + 60);
}

static R Place(Scan s, N n, Dictionary<string, R> rects)
{
    const double GAP = 40, SNAP = 10;
    var nb = s.Edges.Select(e => e.From == n.Id ? e.To : e.To == n.Id ? e.From : null).Where(o => o != null && rects.ContainsKey(o)).Select(o => rects[o]).ToList();
    var par = n.Parent != null && rects.TryGetValue(n.Parent, out var pr) ? pr : null;
    double ix, iy;
    if (nb.Count > 0) { ix = nb.Average(r => r.X + r.W / 2); iy = nb.Average(r => r.Y + r.H / 2); }
    else if (par != null) { ix = par.X + par.W / 2; iy = par.Y + par.H / 2; }
    else { ix = rects.Values.Max(r => r.X + r.W) + 120; iy = 100; }
    if (par != null) { ix = Math.Clamp(ix, par.X - n.W, par.X + par.W + n.W); iy = Math.Clamp(iy, par.Y - n.H, par.Y + par.H + n.H); }
    double bx = Math.Round((ix - n.W / 2) / SNAP) * SNAP, by = Math.Round((iy - n.H / 2) / SNAP) * SNAP;
    var leaves = s.Nodes.Where(o => !o.IsC && rects.ContainsKey(o.Id)).ToList();
    var conts = s.Nodes.Where(o => o.IsC && rects.ContainsKey(o.Id)).ToList();
    bool Ok(R c)
    {
        if (leaves.Any(o => Hit(c, rects[o.Id], GAP))) return false;
        if (n.Parent == null) return !conts.Any(o => Hit(c, rects[o.Id], GAP));
        var P = ContOf(s, s.ById[n.Parent], rects, c);
        if (leaves.Any(o => o.Parent != n.Parent && Hit(P, rects[o.Id], GAP))) return false;
        if (conts.Any(o => o.Id != n.Parent && Hit(P, rects[o.Id], GAP))) return false;
        return P.X >= 20 && P.Y >= 20;
    }
    // ponytail: brute-force ring search over grid slots, fine for a few new boxes per scan
    for (int r = 0; r < 800; r++)
    {
        R best = null; double bd = 1e18;
        for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
            var c = new R(bx + dx * SNAP, by + dy * SNAP, n.W, n.H);
            if (c.X < 20 || c.Y < 20 || !Ok(c)) continue;
            double d = dx * dx + dy * dy; if (d < bd) { bd = d; best = c; }
        }
        if (best != null) return best;
    }
    return new R(bx, by, n.W, n.H);
}

// MSAGL rectilinear router on fixed boxes, then MSAGL edge label placement.
static (List<List<double[]>> routes, List<R> labels, double routeMs, double labelMs) Route(Scan s, Dictionary<string, R> rects)
{
    var (g, _, edges) = Build(s, rects);
    var saved = edges.Select(e => e.Label).ToList();
    foreach (var e in edges) e.Label = null; // labels are placed in a separate, separately timed pass
    var sw = Stopwatch.StartNew();
    // Cluster-aware constructor: padding 4, square corners, sparse visibility graph, 6px between parallel lines.
    // (LayoutHelpers.RouteAndLabelEdges was tried first: it passes only leaf nodes as obstacles, so lines detoured around whole containers.)
    new RectilinearEdgeRouter(g, 4, 0, true, 6).Run();
    double routeMs = sw.Elapsed.TotalMilliseconds;
    for (int i = 0; i < edges.Count; i++) edges[i].Label = saved[i];
    sw.Restart();
    var labels = new List<R>();
    try { new EdgeLabelPlacement(g).Run(); } catch (Exception ex) { Console.Error.WriteLine("label placement threw: " + ex.Message); }
    double labelMs = sw.Elapsed.TotalMilliseconds;
    var routes = new List<List<double[]>>();
    foreach (var e in edges)
    {
        routes.Add(Points(e.Curve));
        var lb = e.Label;
        labels.Add(lb == null || e.Curve == null || (lb.Center.X == 0 && lb.Center.Y == 0) ? null : new R(lb.Center.X - lb.Width / 2, lb.Center.Y - lb.Height / 2, lb.Width, lb.Height));
    }
    return (routes, labels, routeMs, labelMs);
}

static List<double[]> Points(ICurve c)
{
    if (c == null) return null;
    var raw = new List<Point>();
    if (c is Curve cv) { foreach (var seg in cv.Segments) { if (raw.Count == 0) raw.Add(seg.Start); raw.Add(seg.End); } }
    else if (c is Polyline pl) raw.AddRange(pl);
    else { raw.Add(c.Start); raw.Add(c.End); }
    var pts = new List<double[]>();
    foreach (var p in raw)
    {
        var q = new[] { Math.Round(p.X, 2), Math.Round(p.Y, 2) };
        if (pts.Count > 0 && pts[^1][0] == q[0] && pts[^1][1] == q[1]) continue;
        if (pts.Count > 1 && ((pts[^2][0] == pts[^1][0] && pts[^1][0] == q[0]) || (pts[^2][1] == pts[^1][1] && pts[^1][1] == q[1]))) pts[^1] = q; else pts.Add(q);
    }
    return pts;
}

// Same definitions as metrics() in the JavaScript prototype, plus diagonal segments and label clashes.
static (int cross, int share, int through, int boxes, int unrouted, int diag, int labelClash) Metrics(Scan s, Dictionary<string, R> rects, List<List<double[]>> routes, List<R> labels)
{
    var segs = new List<(int i, double x0, double x1, double y0, double y1, bool h)>();
    int diag = 0;
    for (int i = 0; i < routes.Count; i++)
    {
        var r = routes[i]; if (r == null) continue;
        for (int k = 1; k < r.Count; k++)
        {
            double[] a = r[k - 1], b = r[k];
            if (Math.Abs(a[0] - b[0]) > 0.01 && Math.Abs(a[1] - b[1]) > 0.01) diag++;
            segs.Add((i, Math.Min(a[0], b[0]), Math.Max(a[0], b[0]), Math.Min(a[1], b[1]), Math.Max(a[1], b[1]), Math.Abs(a[1] - b[1]) < 0.01));
        }
    }
    int cross = 0, share = 0; const double E = 0.5;
    for (int a = 0; a < segs.Count; a++) for (int b = a + 1; b < segs.Count; b++)
    {
        var p = segs[a]; var q = segs[b]; if (p.i == q.i) continue;
        if (p.h != q.h) { var h = p.h ? p : q; var v = p.h ? q : p; if (v.x0 > h.x0 + E && v.x0 < h.x1 - E && h.y0 > v.y0 + E && h.y0 < v.y1 - E) cross++; }
        else if (p.h) { if (Math.Abs(p.y0 - q.y0) < E && Math.Min(p.x1, q.x1) - Math.Max(p.x0, q.x0) > 4) share++; }
        else if (Math.Abs(p.x0 - q.x0) < E && Math.Min(p.y1, q.y1) - Math.Max(p.y0, q.y0) > 4) share++;
    }
    var leaves = s.Nodes.Where(n => !n.IsC).ToList();
    var pairs = new HashSet<string>();
    foreach (var g in segs)
    {
        var e = s.Edges[g.i];
        foreach (var n in leaves)
        {
            if (n.Id == e.From || n.Id == e.To) continue;
            var r = rects[n.Id];
            if (g.x1 > r.X + 1 && g.x0 < r.X + r.W - 1 && g.y1 > r.Y + 1 && g.y0 < r.Y + r.H - 1) { if (pairs.Add(g.i + "|" + n.Id) && Environment.GetEnvironmentVariable("DBG") != null) Console.Error.WriteLine($"through: {e.From} -> {e.To} crosses {n.Id}"); }
        }
    }
    int boxes = 0;
    for (int a = 0; a < leaves.Count; a++) for (int b = a + 1; b < leaves.Count; b++) if (Hit(rects[leaves[a].Id], rects[leaves[b].Id], -1)) boxes++;
    int clash = 0;
    for (int a = 0; a < labels.Count; a++)
    {
        if (labels[a] == null) continue;
        if (leaves.Any(n => Hit(labels[a], rects[n.Id], -1)) || labels.Where((l, b) => b != a && l != null && Hit(labels[a], l, -1)).Any()) clash++;
    }
    return (cross, share, pairs.Count, boxes, routes.Count(r => r == null || r.Count < 2), diag, clash);
}

static string Svg(Scan s, Dictionary<string, R> rects, List<List<double[]>> routes, List<R> labels, Dictionary<string, R> prev)
{
    string Esc(string t) => t.Replace("&", "&amp;").Replace("<", "&lt;");
    double w = rects.Values.Max(r => r.X + r.W) + 40, h = rects.Values.Max(r => r.Y + r.H) + 40;
    var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w:0}\" height=\"{h:0}\" viewBox=\"0 0 {w:0} {h:0}\" font-family=\"sans-serif\" font-size=\"11\"><rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>\n");
    foreach (var n in s.Nodes.Where(n => n.IsC))
    {
        var r = rects[n.Id];
        sb.Append($"<rect x=\"{r.X}\" y=\"{r.Y}\" width=\"{r.W}\" height=\"{r.H}\" fill=\"#f3f1ea\" stroke=\"#8a8370\" stroke-dasharray=\"6 3\"/><text x=\"{r.X + 8}\" y=\"{r.Y + 16}\" font-weight=\"bold\">{Esc(n.Name)}</text>\n");
    }
    foreach (var r in routes.Where(r => r != null && r.Count > 1))
        sb.Append($"<polyline points=\"{string.Join(" ", r.Select(p => $"{p[0]},{p[1]}"))}\" fill=\"none\" stroke=\"#35506b\" stroke-width=\"1.2\"/><circle cx=\"{r[^1][0]}\" cy=\"{r[^1][1]}\" r=\"2.5\" fill=\"#35506b\"/>\n");
    foreach (var n in s.Nodes.Where(n => !n.IsC))
    {
        var r = rects[n.Id];
        bool isNew = prev != null && !prev.ContainsKey(n.Id);
        sb.Append($"<rect x=\"{r.X}\" y=\"{r.Y}\" width=\"{r.W}\" height=\"{r.H}\" fill=\"{(isNew ? "#ffe9a8" : "#fff")}\" stroke=\"#222\"/><text x=\"{r.X + r.W / 2}\" y=\"{r.Y + 18}\" text-anchor=\"middle\">{Esc(n.Name)}</text><text x=\"{r.X + r.W / 2}\" y=\"{r.Y + 32}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#666\">{Esc(n.Kind)}</text>\n");
    }
    for (int i = 0; i < labels.Count; i++)
    {
        var l = labels[i]; if (l == null) continue;
        sb.Append($"<rect x=\"{l.X:0.#}\" y=\"{l.Y:0.#}\" width=\"{l.W}\" height=\"{l.H}\" fill=\"#fff\" fill-opacity=\"0.85\" stroke=\"#c33\" stroke-width=\"0.4\"/><text x=\"{l.X + l.W / 2:0.#}\" y=\"{l.Y + 9.5:0.#}\" text-anchor=\"middle\" font-size=\"9\" fill=\"#c33\">{Esc(s.Edges[i].Label)}</text>\n");
    }
    return sb.Append("</svg>\n").ToString();
}

record R(double X, double Y, double W, double H);
record E(string From, string To, string Label);
class N { public string Id, Name, Kind, Parent; public bool IsC; public double W, H; }
class Scan { public List<N> Nodes = new(); public List<E> Edges = new(); public Dictionary<string, N> ById; }
static class Geo
{
    public static double Snap(double v) => Math.Round(v / 10) * 10;
    public static bool Hit(R a, R b, double m) => a.X - m < b.X + b.W && a.X + a.W + m > b.X && a.Y - m < b.Y + b.H && a.Y + a.H + m > b.Y;
    public static R ToR(Rectangle b) => new(b.Left, b.Bottom, b.Width, b.Height);
}
