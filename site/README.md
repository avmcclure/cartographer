# The site

A static Astro site drawn from one atlas. It reads only the JSON that `cartographer site prepare` writes; nothing else is passed to it.

```
cartographer site build --atlas <atlas> --out <dir>      # prepare, npm ci, astro build; needs Node.js 22+
```

Options: `--edit-url <url>` adds "edit this file" links (for example `https://github.com/acme/atlas/edit/main`); `--base <path>` is the path the site is served under when that is not `/`.

Working on the site itself, from the repository root:

```
dotnet run --project src/Cartographer.Cli -- site prepare --atlas fixtures/sample-atlas --out site/data
cd site && npm ci && npm run dev
```

`CARTOGRAPHER_DATA` points the site at another data directory (default `data/`).

## Pages

| Page | Path |
|---|---|
| Organisation index | `/` |
| System plan, area plan | `/sheet/<sheet id>/` |
| Gazetteer | `/gazetteer/` |
| Entry, with a locator | `/entry/<entry id>/` |
| Data tags, and a plan per tag | `/tags/`, `/tags/<tag>/` |
| Questions that need a person | `/questions/` |
| Recent changes | `/changes/` |

The selection is carried in the address: `?sel=<entry id or group key>`, and `&down=1` when it is extended downstream. Pages read without JavaScript; scripts add selection, panning and the gazetteer's filter.

## Data

All files are deterministic: sorted, no timestamps. Positions and sizes on sheets are whole grid steps (the site draws 10 px a step).

- `site.json`: `organisation`, `cartographer`, `latestVerified`, `staleAfterDays`, `editable`, and `sheets` (`id`, `title`, `level`, `subject`, `up`, `plots`, `unplaced`) in ID order. A sheet's number is its place in that list.
- `sheets/<sheet id>.json`, one per sheet. Sheet IDs are `organisation`, `system/<system>` (also `system/unassigned`, and `system/<prefix>` for provisional systems) and `area/repositories/<folder>` or `area/databases/<folder>`.
  - `level`, `title`, `subject`, `up`, `width`, `height`, `cell` (steps per grid square), `inside` (parts are drawn inside plots), `note`, `unplaced` (present when the sheet needs a re-survey: names of plots left off).
  - `plots`: `id`, `entry`, `number`, `ref` (grid reference), `name`, `kind`, `type`, `sub`, `summary`, `parent`, `container`, `neighbour` (belongs to another sheet), `provisional`, `retired`, `drift`, `unsurveyed`, `parts`, `uses`, `usedBy`, `opens` (the sheet one level down), `x`, `y`, `w`, `h`.
  - `lines`: the drawn, summarised lines. `id`, `from`, `to` (plot IDs; one is absent for a line that runs to the sheet edge, which then has `beyond` and `beyondSheet`), `text`, `rows` (the text as lettered), `basis` (the weakest: inferred, else extracted, else stated), `stated` (some link was stated by a person), `total`, `verbs` (`verb`, `count`), `pairs` (the links it stands for, as `[from entry, to entry]`), `points` (corners, source first), `label` (`[x, y, w, h]`).
  - `links`: the links schedule, every link the sheet covers: `from`, `fromName`, `verb`, `to`, `toName`, `basis`, `evidence` (`label`, `url`), `line` (the drawn line that carries it, if any).
  - `routes`: part ID to the routes of that part's own links (`from`, `to`, `text`, `basis`, `points`, `label`), drawn only while the part is selected. Present on system sheets that show parts inside plots.
  - `where`: entry ID to the plot IDs that stand for it on this sheet.
- `entries/<entry id>.json`, one per entry: the frontmatter facts with every evidence pointer resolved to `{label, url}`; `uses` (its links) and `usedBy` (inbound links, with `heading`); `sections` (`title`, `markdown`); `questions` and `dismissed`; `verified` and `stale`; `home` (`sheet`, `plot`, `number`, `ref`: where it is drawn as a plot), `opens` (its own sheet), `editUrl`.
- `gazetteer.json`: every entry with `id`, `name`, `kind`, `type`, `summary`, `status`, `sheet`, `number`, `ref`, `unsurveyed`, `verified`, `stale`.
- `graph.json`: `entries` (ID to `[name, kind]`), `links` (`[from, to, type, basis]`), `groups` (group key to the entries it stands for). Used in the browser to work out what a selection reaches.
- `questions.json`: every open, needs-person and answered question with its entry and `editUrl`.
- `changes.json`: change summaries from `changes/`, newest first: `date`, `repository`, `commit`, `title`, `markdown`.
- `tags.json`: each business-data tag with its `tables` and the system `sheets` they are drawn on.

An evidence `url` points at the file and lines at the repository's primary location: `github.com/<org>/<repo>/blob/<commit>/<path>#L..` or the Azure DevOps equivalent.

## Layouts

`site prepare` keeps one Groma layout file per sheet in the atlas, at `layouts/<sheet id>.layout.json`. A plot that is in the file never moves. When a new or grown plot cannot be fitted without moving another, the sheet's layout is left as it was, the sheet is drawn without the plots that did not fit, and the command exits 1; `cartographer plan resurvey <sheet id>` then lays the sheet out afresh, which changes its plot numbers and grid references.

`screenshots/` holds pictures of every page type built from `fixtures/sample-atlas`.
