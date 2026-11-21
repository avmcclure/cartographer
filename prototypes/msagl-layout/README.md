# MSAGL layout (throwaway trial)

**This is a throwaway trial for issue #29.** It asks whether MSAGL can do the survey-plan layout inside a .NET CLI: first layout, kept positions across scans, right-angled lines around fixed boxes. Nothing here is meant to be kept.

Run: `dotnet run -c Release` in this folder. It reads `real.json` and `large.json` (the graphs exported unchanged from `prototypes/plan-layout/index.html` on `prototype/plan-layout`), prints the table below and writes one SVG per sheet and scan into `out/`.

## Package

- NuGet `Msagl` 1.2.1 (published December 2024, one assembly `Microsoft.Msagl.dll`, target `netstandard2.0`).
- Restored and ran on .NET SDK 10.0.203 with `net10.0` as the target framework. No workarounds, no warnings from the package, no native dependencies. It was the first package tried; the older `AutomaticGraphLayout` 1.1.12 and `Microsoft.Msagl` 1.1.6 were not needed.

## What was done

- **Scan 1**: `SugiyamaLayoutSettings` through `LayoutHelpers.CalculateLayout`, databases as `Cluster`s. Box positions snapped to a 10 px grid and stored.
- **Scans 2 and 3**: every surviving box keeps its stored position; new boxes go to the nearest free grid slot near their linked neighbours, inside or beside their database. This placement is own code, ported from the JavaScript prototype.
- **Every scan**: all lines routed by `RectilinearEdgeRouter` on the fixed boxes, then labels placed by `EdgeLabelPlacement`.

## Measured results

macOS, Release build, 2026-11-20. Same metric definitions as the JavaScript prototype (distances in 120 px cells; "moved" counts leaf boxes present in both scans). JavaScript numbers are from that prototype's README.

| Sheet | Step | MSAGL moved | MSAGL crossings | JS kept positions crossings | JS fresh ELK crossings | Lines through boxes | Lines on top of each other | Overlapping boxes | Unrouted | Layout ms | Routing ms | Labels ms |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Real | Scan 1 | | 11 | 17 | 9 | 0 | 0 | 0 | 0 | 72 | 84 | 2 |
| Real | 1 to 2 | 0 of 19 | 20 | 30 | 12 | 0 | 0 | 0 | 0 | 6 | 17 | 2 |
| Real | 2 to 3 | 0 of 20 | 23 | 22 | 10 | 0 | 0 | 0 | 0 | 0 | 17 | 2 |
| Large | Scan 1 | | 1032 | 692 | 974 | 0 | 0 | 0 | 0 | 105 | 257 | 24 |
| Large | 1 to 2 | 0 of 79 | 1025 | 710 | 1029 | 0 | 38 | 0 | 0 | 7 | 274 | 22 |
| Large | 2 to 3 | 0 of 81 | 901 | 675 | 851 | 0 | 36 | 0 | 0 | 1 | 216 | 23 |

Average and maximum distance moved are 0 in every kept-positions step, as in the JavaScript prototype. No diagonal segments in any drawing. The first row's times include .NET start-up compilation. "Layout ms" on later scans is the own placement code.

The crossings are not a like-for-like router comparison: scan 1 here comes from MSAGL's layout, the JavaScript one from ELK's, and box widths differ slightly (the prototype's second text line is approximated).

## What worked through MSAGL itself, and what did not

| Need | Result |
|---|---|
| Layered first layout | MSAGL. Works. |
| Database containers | MSAGL clusters. Works, but each cluster is laid out on its own and then packed as one block, so the large sheet comes out tall (2420 x 4960 px) and links into a database do not shape its inside. `CalculateLayout` throws a null error unless the root cluster is given `UserData`. |
| Routing around fixed boxes | MSAGL `RectilinearEdgeRouter(GeometryGraph, padding, cornerRadius, sparse, edgeSeparation)`. Works on boxes placed by hand: all lines routed, right-angled, none through a box. It knows the cluster hierarchy. The obvious helper `LayoutHelpers.RouteAndLabelEdges` does not: it gave 25 to 48 crossings on the real sheet, with lines detouring around whole databases. |
| Placing only new boxes | **Own code.** MSAGL's `Relayout` (given the new boxes as `addedNodes`) moved 18 of 19 and 19 of 20 surviving boxes on the real sheet and 78 of 79 and 80 of 81 on the large one, measured after discounting a uniform shift, and left 3 overlapping pairs once. It threw a null error until clusters were given a `RectangularBoundary`. Not usable for kept positions. |
| Edge labels | MSAGL `EdgeLabelPlacement`, run after routing. Every label got a position (2 of 31 clash with a box or another label on real scan 3, none elsewhere). Labels are put beside the line, not on it, so in busy areas it is unclear which line a label belongs to; labels over unrelated lines were not measured. |

## Reading of the drawings

Looked at `out/real-scan2.svg` and `out/large-scan2.svg` rendered to PNG.

- Real sheet: clean. Right-angled lines, parallel lines kept apart, no line through a box, databases respected. Better than the prototype's crude router (20 crossings against 30) and close to ELK's fresh layout.
- Large sheet: still a hairball, about 1000 crossings, worse than the prototype's router (about 700). Lines run freely through databases they have nothing to do with; the router treats a foreign cluster as open ground. On scans 2 and 3 about 37 pairs of lines run on top of each other, which did not happen on scan 1.
- Labels are legible on the real sheet and mostly noise on the large one.

## Not done or not verified

- Only three scans; slow degradation under many incremental placements is not measured.
- Router options were not tuned (bend penalty, edge separation, making foreign clusters cost something).
- Ports, lines leaving the sheet edge, nested clusters deeper than one level, a new database appearing in a later scan.
- Windows and Linux; trimming or AOT publishing with this `netstandard2.0` assembly.
- The package has had no release since December 2024.

## Size

348 lines of C# in one file (`Program.cs`), of which about 60 are the ported placement, 45 the metrics and 30 the SVG writer. The MSAGL calls themselves are about 60 lines.
