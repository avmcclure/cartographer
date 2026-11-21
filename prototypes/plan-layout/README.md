# Plan layout (throwaway prototype)

**This is a throwaway prototype for issue #20.** It asks whether a survey plan can be drawn automatically from real entries and still read well, and whether box positions hold when a few things change between scans. Nothing here is meant to be kept.

`index.html` is one self-contained page (no doctype or body tags, because it is published as an Artifact fragment). To open it locally, wrap it in a minimal HTML shell. It loads elkjs 0.9.3 from jsDelivr; everything else is inline.

## What is in it

- **Real sheet**: the CIOC system from `prototype/atlas-sample`. The graph was built from the entries' frontmatter `links:` (22 boxes, 26 links), including link targets with no entry file, marked "not yet surveyed". One link was added by hand: the trigger write from `NAICS_Example`, which the sample records in body text only. Scans 2 and 3 are invented changes to those entries.
- **Large sheet (synthetic)**: 85 boxes, 155 links, seeded generator, three scans.
- **Organisation index (synthetic)**: 13 boxes, 21 counted links, one scan.

## What was tried

- **Fresh**: ELK layered, orthogonal routing, database containers, run from scratch on each scan.
- **Kept positions**: scan 1 from ELK, leaf boxes snapped to a 20 px grid and stored. Later scans keep every surviving box where it was, place new boxes in the nearest free grid slot to their linked neighbours (inside or beside their database), and re-route every line.
- **ELK seeded**: ELK with the `INTERACTIVE` strategies for cycle breaking, layering, crossing minimisation and node placement, fed the previous positions. This was the "try ELK first" attempt at stability.

ELK in the browser has no way to route lines around boxes that are already placed (the `fixed` algorithm does not route, and the interactive strategies recompute coordinates). So Kept positions uses a crude router written here: A* on a 10 px grid, one line at a time, shortest first, with penalties for bends, crossings, running along another line and cutting through an unrelated database. No rip-up, no nudging, no shared trunks.

## Measured results

Chromium via Playwright, 2026-11-20. Distances in grid cells (one cell is 120 px). "Moved" counts leaf boxes present in both scans. No drawing had a line through a box, two lines on top of each other, overlapping boxes or an unrouted line.

| Sheet | Step | Layout | Moved | Average | Maximum | Crossings |
|---|---|---|---|---|---|---|
| Real | Scan 1 | Fresh | | | | 9 |
| Real | Scan 1 | Kept positions | | | | 17 |
| Real | 1 to 2 | Fresh | 19 of 19 | 1.23 | 3.72 | 12 |
| Real | 1 to 2 | Kept positions | 0 of 19 | 0 | 0 | 30 |
| Real | 1 to 2 | ELK seeded | 18 of 19 | 3.68 | 6.33 | 145 |
| Real | 2 to 3 | Fresh | 20 of 20 | 1.45 | 3.41 | 10 |
| Real | 2 to 3 | Kept positions | 0 of 20 | 0 | 0 | 22 |
| Real | 2 to 3 | ELK seeded | 20 of 20 | 0.60 | 1.34 | 150 |
| Large | Scan 1 | Fresh | | | | 974 |
| Large | Scan 1 | Kept positions | | | | 692 |
| Large | 1 to 2 | Fresh | 79 of 79 | 15.15 | 24.84 | 1029 |
| Large | 1 to 2 | Kept positions | 0 of 79 | 0 | 0 | 710 |
| Large | 1 to 2 | ELK seeded | 75 of 79 | 23.42 | 35.46 | 7586 |
| Large | 2 to 3 | Fresh | 81 of 81 | 9.89 | 20.52 | 851 |
| Large | 2 to 3 | Kept positions | 0 of 81 | 0 | 0 | 675 |
| Large | 2 to 3 | ELK seeded | 81 of 81 | 7.72 | 16.50 | 8898 |
| Index | Scan 1 | Fresh | | | | 6 |
| Index | Scan 1 | Kept positions | | | | 5 |

With Kept positions, one or two database outlines grow or shrink per scan when a table is added or removed at their edge. Boxes inside do not move.

## Reading of the results

- A fresh layout moves every box on every scan, even for two or three changes. Grid references would not survive a scan.
- Kept positions holds every box by construction. On the real sheet the crude router costs about twice the crossings of ELK's own routing. On the large sheet it has fewer crossings than ELK, mostly because it may leave a box from any side.
- Seeding ELK with old positions did not help: nearly every box moved and the lines tangled. The option set may not be the best possible; it was not tuned.
- About 20 boxes reads well. About 85 boxes with 150 links is a hairball in every strategy (700 to 1000 crossings); it is usable only with the selection highlight and the schedule. A sheet that size needs splitting or filtering, not a better router.
- At phone width a sheet does not fit at a readable size.

## Not verified

- Only Chromium was tested. No real phone, no Safari.
- Only three scans. Whether a sheet degrades after many incremental placements is not measured.
- Boxes that change size between scans, new databases, and nested containers are handled naively or not at all.
