# 1. Interaction clusters replace plans, sheets and Groma

Date: 2026-11-23. Status: accepted.

## Context

The site drew the atlas as cartographic plans: an organisation sheet, a sheet per system and per area, numbered plots, grid references and a gazetteer. Layout came from Groma, our own orthogonal layout library, with one stored layout per sheet.

On a real atlas this did not work. One organisation sheet with 42 plots and 120 links on 41 lines came out about three times taller than wide, and its busiest system grew a wall of parallel tracks. Grouping by system, which mostly means by owner, does not match how parts talk to each other: one monorepo had parts in nearly every group of parts that work together. The cartography vocabulary also has to be learned before the map can be used.

Prototypes of the replacement were reviewed: `prototypes/clusters.src.html` (clusters as cards) and then `prototypes/flow.src.html` (lanes with data flowing through them), published as the "Atlas Clusters" artifact.

## Decision

- **Clusters come from links, not owners.** Cartographer groups parts with Louvain community detection over the link graph, with one weight per link. Code dependencies (`depends-on`) are left out of clustering and off the map: a shared library would pull together parts that never exchange anything. Parts with no other links share one group, "Not linked yet". Topics, databases and external systems belong to the cluster that uses them most. Owners and system entries are shown as facts about a part ("Found in"), never as the grouping.
- **People can pin clusters.** A pinned cluster keeps its members and name across scans. Every scan also clusters the current links afresh. When the fresh grouping keeps at least 3 percentage points more links inside clusters than the pinned one, and some part would move, Cartographer suggests a regroup. It says what would merge or move. Pinned clusters live in the atlas as `clusters.yaml`, written by the CLI. The site is static, so a regroup is applied through the CLI, not from the page.
- **One map, with the data flowing through it.** Clustering keeps like things close; it does not isolate them. The map shows every part at once, one horizontal lane per cluster, and arrows follow the data rather than the code dependency: writing and publishing point into the table or topic, reading and consuming point out of it. Calls carry requests, not data, and are drawn dotted. Within a lane each part sits one column right of the furthest part in that lane that feeds it. Selecting a part lights up everything that feeds it and everything its data reaches, across lanes. There are no cluster pages, sheets, plot numbers, grid references or gazetteer; an "Everything" table and search sit beside the map, and each part keeps its own page.
- **Layout is worked out when the site is built.** Lanes and columns follow from the clusters and links, so the same atlas draws the same map. Nothing about positions is stored, and `layouts/` goes away.
- **Groma is retired.** Cartographer drops the package, `Surveyor`, the sheet model and `plan resurvey`.

## Consequences

- Positions are no longer guaranteed stable between scans. Stability comes from pinned clusters, not from coordinates.
- Stored `layouts/*.layout.json` files in existing atlases become dead and can be deleted.
- Spec section 8, `docs/using.md`, `PRODUCT.md`, the site README and the setup skill need rewriting.
- Atlases grow to hundreds of repositories and thousands of parts. Above 80 parts, lanes of more than 6 parts start closed, showing one box per column for the parts in it, and open when a reader selects the lane or a part in it. Columns are counted within a lane, so the map's width follows the deepest lane, not the longest chain across the whole estate. Checked against a generated estate of 850 repositories and 1,700 parts (`tools/make-enterprise-atlas.py`): there, following one service's data all the way reached 1,444 parts through shared databases and the data platform, so a trace stops at two steps unless the reader asks for more, and links between clusters stay hidden until a part is selected.
