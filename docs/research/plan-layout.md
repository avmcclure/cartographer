# Layout methods for plans of boxes and right-angled lines

Research for [#19](https://github.com/avmcclure/cartographer/issues/19). Checked against primary sources on 2026-11-20. Nothing here was run; every behaviour is as documented, not as measured.

## Answer

No library surveyed documents a guarantee that positions stay put when the graph changes. Each offers hints at best: ELK re-derives layers and order from supplied coordinates, Graphviz and MSAGL can route edges around boxes whose positions are given, dagre offers nothing.

So stability has to come from Cartographer: commit a layout file per sheet in the atlas, treat stored positions as fixed, place only new boxes, and re-route the lines on every scan. The library is then needed for two jobs: a good first layout, and right-angled routing around fixed boxes.

- **MSAGL** does both inside the .NET CLI (layered layout, a rectilinear router that takes obstacles, MIT). Its .NET package is thinly maintained.
- **ELK (elkjs)** is the best match for the drawing itself (nested boxes, ports, edge labels, orthogonal routing) and is actively maintained, but it runs in Node, not .NET, and cannot route around pinned boxes in elkjs.
- **Graphviz** can route around pinned boxes (`neato -n`), but its orthogonal router ignores ports and needs a native binary or WASM.
- **dagre** has no orthogonal routing and no stability features.

Prototype first: MSAGL in the CLI with a committed layout file. Main risk: the quality of MSAGL's routing on fixed, grid-snapped boxes, and layouts degrading as incremental placements accumulate.

## Comparison

| | ELK / elkjs | Graphviz | dagre | MSAGL (.NET) |
|---|---|---|---|---|
| Algorithm family | Layered (Sugiyama) [1] | `dot`: layered; `neato`: stress majorization [9] | Layered [14] | Layered (Sugiyama) with constraints; also a force-directed incremental layout [17][20] |
| Nested boxes | Yes: `compound` and `clusters` features; cross-hierarchy edges with `hierarchyHandling=INCLUDE_CHILDREN` [1][3] | Clusters, "by certain layout engines" [10]; which ones besides `dot`: unverified | Clustering is implemented (`nesting-graph.ts`), not documented as options [14][15] | Clusters, laid out recursively [19] |
| Right-angled routing | `edgeRouting=ORTHOGONAL`, respects port constraints [2][4] | `splines=ortho`; "does not handle ports or, in dot, edge labels" [6] | No: edges come back as control points; no orthogonal option documented [14] | `EdgeRoutingMode.Rectilinear`; `RectilinearEdgeRouter` takes a set of obstacle shapes [16][18] |
| Edge labels | Yes (`edge_labels` feature) [1] | Not with ortho in `dot`; `xlabel` places labels afterwards and may fail to place some [6][7] | Yes: label width, height, `labelpos` [14] | Unverified for the rectilinear router |
| Ports | Yes [1][2] | Not with ortho [6] | No | Router chooses ports ("optimal port"), or routes to centres [16] |
| Stability features | `INTERACTIVE` strategies for cycle breaking, layering, crossing minimisation and node placement read existing positions; `semiInteractive`; model-order options; `fixed` algorithm keeps given positions but does not route [1][2][4][5] | `pos` + `pin` in `neato`/`fdp`; `neato -n` takes all positions as given and only routes edges. `pos` input is "neato, fdp only", so `dot` cannot be seeded [8][9][11] | None documented [14] | Layer constraints (`PinNodesToSameLayer`, `AddUpDownConstraint`, `AddLeftRightConstraint`…); `Relayout` for added/modified nodes; `LockPosition` in the incremental layout; routing on given positions [17][18][19][20] |
| Runs in | Node (elkjs) and browser; Java otherwise. Not .NET [12] | Native binary anywhere; Node via WASM [13] | Node / browser | .NET Standard 2.0 in-process [21]; separate JS port msagljs [22] |
| Licence | elkjs: `EPL-2.0 OR GPL-3.0-or-later` [12] | EPL-2.0 [23]; WASM wrapper Apache-2.0 [13] | MIT [15] | MIT [24] |
| Maintenance | elkjs 0.12.1, 2026-10-28 [12] | 16.1.1, 2026-11-08 [25] | `@dagrejs/dagre` 3.1.3, Oct 2026; the unscoped `dagre` package is not updated [15] | NuGet `Msagl` 1.2.1, 2024-12-14 [21]; repo commits in 2026 are mostly upkeep [26]; msagljs is active [22] |

## Detail

### ELK (elkjs)

ELK Layered "supports different routing styles (straight, orthogonal, splines); if orthogonal routing is selected, arbitrary port constraints are respected", and does "full layout of compound graphs with cross-hierarchy edges" [2]. It is the closest fit to the survey-plan drawing.

For stability it offers seeding, not pinning:

- Each of the four phases has an `INTERACTIVE` strategy that reads the coordinates in the input graph. Layering: "Nodes are put into layers according to their relative position." Node placement: "leaves y coordinates of nodes untouched." [4]
- `crossingMinimization.semiInteractive` "preserves the order of nodes within a layer", taking the order from `elk.position` [2].
- `considerModelOrder.strategy` keeps the input order of nodes and edges where that adds no crossings [2].
- `layerChoiceConstraint` and `positionChoiceConstraint` are "only evaluated as part of the `InteractiveLayeredGraphVisitor`, which must be applied manually" [2]. Whether elkjs applies it is unverified.

Limits: these preserve order and layer membership; coordinates are still recomputed, so a new box can shift a row or column. The `fixed` algorithm "keeps the current layout as it is" with optional bend points, which means it does not route [5]. ELK has a Libavoid plugin for routing, but elkjs ships only `layered`, `stress`, `mrtree`, `radial`, `force`, `disco`, `box`, `fixed`, `random` [12]. elkjs is the Java code compiled with GWT, so the options above should behave the same, but that is untested.

### Graphviz

`dot` produces layered layouts with clusters. `splines=ortho` gives axis-aligned routes but "does not handle ports or, in dot, edge labels" [6]. `dot` does not read `pos` on input [8], so it cannot be seeded or pinned: every run is a fresh layout.

The stable path is `neato -n`: "neato assumes nodes have already been positioned … computes the edge layouts, depending on the value of the splines attribute" [11]. That is routing around fixed boxes. Whether `splines=ortho` under `neato -n` gives acceptable routes and labels, and how clusters are drawn there, is unverified. From .NET it needs the native binary as a child process; in Node it runs as WASM [13].

### dagre

Layered layout returning polyline control points, with edge-label boxes [14]. No orthogonal routing, pinning, seeding or incremental option is documented [14]. It can only supply a first layout, and ELK does that with more of what the plans need.

### MSAGL and other .NET options

MSAGL is the only credible in-process .NET option found. It has a Sugiyama layout with user constraints on layers and left/right order [17], a rectilinear edge router that can be constructed directly from "the collection of shapes to route around" [18], and a `Relayout` class that takes `modifiedNodes` and `addedNodes`, where "a new initial position will be found for these nodes close to their neighbors" and clusters are "laid-out in place" [19]. The force-directed `FastIncrementalLayout` supports locked positions [20], but a force-directed result does not suit a gridded plan.

Limits: the NuGet package (`Msagl`, .NET Standard 2.0) was last published in December 2024 [21]; edge-label handling in the rectilinear router and routing quality across cluster boundaries are unverified. msagljs is a separate, active TypeScript implementation containing the same router [22], which gives a Node fallback. GraphShape (MIT, last push January 2024) was not evaluated [27]. No other maintained .NET library with orthogonal routing was found.

### Persisting coordinates in the atlas

One committed file per sheet, keyed by entry id, holding each box's grid cell (and size). On each scan:

1. Boxes already in the file keep their cell.
2. New boxes are placed in a free cell near their neighbours.
3. Removed boxes free their cell; nothing else moves.
4. All lines are re-routed around the boxes. Routes are derived, so they need not be committed.

Benefits: stability is exact and independent of any library's behaviour or version; a move shows up as a reviewable diff; a person can hand-correct a sheet; grid references stay valid.

Costs: Cartographer owns a placement rule for new boxes; a sheet's layout slowly gets worse as placements accumulate, so a deliberate "re-survey" command that re-lays out a sheet (and breaks its references) is needed; a box that grows (a database gaining tables) can collide with neighbours; the file must be written where the atlas is written, which favours doing layout in the CLI over the Astro build.

## Recommendation

Prototype first: in the .NET CLI, use MSAGL's layered layout for a sheet's first placement, snap to the grid, write the layout file, then on a second scan with a few boxes and links changed keep stored cells, place the new boxes, and re-route with `RectilinearEdgeRouter`. Run it on the 3-box and 80-box cases and diff the two layout files. Use elkjs (layered, orthogonal, `INCLUDE_CHILDREN`) on the same graphs as the benchmark for how good the first layout could look.

Main risk: MSAGL's router may not give clean, labelled, right-angled lines on fixed grid-snapped boxes (labels, shared ports, lines leaving the sheet edge are all unverified), on a .NET package with little recent release activity. If it fails, the fallback is routing in Node with msagljs or elkjs, which moves the layout step out of the CLI. The second risk is independent of library: incremental placement degrading a sheet over time.

## Sources

1. https://eclipse.dev/elk/reference/algorithms/org-eclipse-elk-layered.html
2. https://github.com/eclipse-elk/elk/blob/master/plugins/org.eclipse.elk.alg.layered/src/org/eclipse/elk/alg/layered/Layered.melk
3. https://eclipse.dev/elk/reference/options/org-eclipse-elk-hierarchyHandling.html
4. https://github.com/eclipse-elk/elk/tree/master/plugins/org.eclipse.elk.alg.layered/src/org/eclipse/elk/alg/layered/options (`CycleBreakingStrategy.java`, `LayeringStrategy.java`, `CrossingMinimizationStrategy.java`, `NodePlacementStrategy.java`); `EdgeRouting` values in https://github.com/eclipse-elk/elk/blob/master/plugins/org.eclipse.elk.core/src/org/eclipse/elk/core/options/EdgeRouting.java
5. https://eclipse.dev/elk/reference/algorithms/org-eclipse-elk-fixed.html
6. https://graphviz.org/docs/attrs/splines/
7. https://graphviz.org/docs/attrs/xlabel/
8. https://graphviz.org/docs/attrs/pos/ and https://graphviz.org/docs/attrs/pin/
9. https://graphviz.org/docs/layouts/neato/
10. https://graphviz.org/doc/info/lang.html
11. https://graphviz.org/doc/info/command.html (`-n`); https://graphviz.org/docs/layouts/nop2/
12. https://github.com/kieler/elkjs/blob/master/README.md ; https://github.com/kieler/elkjs/blob/master/package.json ; https://github.com/kieler/elkjs/releases
13. https://github.com/hpcc-systems/hpcc-js-wasm (package `@hpcc-js/wasm-graphviz`)
14. https://github.com/dagrejs/dagre/wiki
15. https://github.com/dagrejs/dagre (README, `lib/`); https://www.npmjs.com/package/@dagrejs/dagre
16. https://github.com/microsoft/automatic-graph-layout/blob/master/GraphLayout/MSAGL/Core/Routing/EdgeRoutingMode.cs
17. https://github.com/microsoft/automatic-graph-layout/blob/master/GraphLayout/MSAGL/Layout/Layered/SugiyamaLayoutSettings.cs
18. https://github.com/microsoft/automatic-graph-layout/blob/master/GraphLayout/MSAGL/Routing/Rectilinear/RectilinearEdgeRouter.cs
19. https://github.com/microsoft/automatic-graph-layout/blob/master/GraphLayout/MSAGL/Layout/Incremental/Relayout.cs
20. https://github.com/microsoft/automatic-graph-layout/blob/master/GraphLayout/MSAGL/Layout/Incremental/FastIncrementalLayout.cs and `LockPosition.cs` beside it
21. https://www.nuget.org/packages/Msagl/
22. https://github.com/microsoft/msagljs (`modules/core/src/routing/rectilinear/RectilinearEdgeRouter.ts`); https://www.npmjs.com/package/@msagl/core
23. https://graphviz.org/license/
24. https://github.com/microsoft/automatic-graph-layout/blob/master/LICENSE
25. https://gitlab.com/graphviz/graphviz/-/releases
26. https://github.com/microsoft/automatic-graph-layout/commits/master
27. https://github.com/KeRNeLith/GraphShape
