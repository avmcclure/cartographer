# 1. Interaction clusters replace plans, sheets and Groma

Date: 2026-11-23. Status: accepted.

## Context

The site drew the atlas as cartographic plans: an organisation sheet, a sheet per system and per area, numbered plots, grid references and a gazetteer. Layout came from Groma, our own orthogonal layout library, with one stored layout per sheet.

On a real atlas this did not work. One organisation sheet with 42 plots and 120 links on 41 lines came out about three times taller than wide, and its busiest system grew a wall of parallel tracks. Grouping by system, which mostly means by owner, does not match how parts talk to each other: one monorepo had parts in nearly every group of parts that work together. The cartography vocabulary also has to be learned before the map can be used.

A prototype of the replacement was reviewed: `prototypes/clusters.html` (published as the "Atlas Clusters" artifact).

## Decision

- **Clusters come from links, not owners.** Cartographer groups parts with Louvain community detection over the link graph, with one weight per link. Topics, databases and external systems belong to the cluster that uses them most. Owners and system entries are shown as facts about a part ("Found in"), never as the grouping.
- **People can pin clusters.** A pinned cluster keeps its members and name across scans. Every scan also clusters the current links afresh. When the fresh grouping keeps at least 3 percentage points more links inside clusters than the pinned one, and some part would move, Cartographer suggests a regroup. It says what would merge or move. Pinned clusters live in the atlas as `clusters.yaml`, written by the CLI. The site is static, so a regroup is applied through the CLI, not from the page.
- **One map you open.** The site has three views: a map of clusters with link counts between them; a cluster, showing its parts and the outside parts they link to; and an "Everything" table with search. A part's page lists what it uses and what uses it. There are no sheets, plot numbers, grid references or gazetteer.
- **Layout is drawn in the browser.** The map is laid out with a seeded force simulation (d3-force) from the cluster data. Nothing about positions is stored, and `layouts/` goes away.
- **Groma is retired.** Cartographer drops the package, `Surveyor`, the sheet model and `plan resurvey`.

## Consequences

- Positions are no longer guaranteed stable between scans. Stability comes from pinned clusters, not from coordinates.
- Stored `layouts/*.layout.json` files in existing atlases become dead and can be deleted.
- Spec section 8, `docs/using.md`, `PRODUCT.md`, the site README and the setup skill need rewriting.
- The force layout is fine for tens of clusters and for a cluster of up to a few hundred parts. Beyond that, a cluster would be split into sub-clusters by running Louvain again inside it.
