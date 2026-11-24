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
| The map of clusters | `/` |
| One cluster | `/cluster/<cluster key>/` |
| Everything, A to Z | `/everything/` |
| Entry | `/entry/<entry id>/` |
| Data tags, and one page per tag | `/tags/`, `/tags/<tag>/` |
| Questions that need a person | `/questions/` |
| Recent changes | `/changes/` |

Pages read without JavaScript: the clusters and their parts are listed beside each drawing. Scripts draw the map and a cluster (a force layout run to rest, so the same data draws the same picture), trace a selected part, filter the Everything table and run the search box.

## Data

All files are deterministic: sorted, no timestamps.

- `site.json`: `organisation`, `cartographer`, `latestVerified`, `staleAfterDays`, `editable`.
- `clusters.json`: the map (ADR 1).
  - `links` (links between parts), `inside` (the share of them inside a cluster).
  - `clusters`: `key` (for the address; stable only while the cluster is pinned, since an unpinned cluster is named after its busiest part), `name`, `pinned`, `members` (`id`, `name`, `kind`, `type`, `found`: the repository or database it was found in, `unsurveyed`).
  - `parts`: links between two parts, one per direction: `from`, `to`, `count`, `basis` (the weakest), `verbs` (`verb`, `count`; most common first). A link to a table, view, routine or trigger counts as one to its database.
  - `suggestion`: present when regrouping would keep at least 3 percentage points more links inside clusters: `inside`, `moved` (`part`, `from`, `to`), `gone` (pinned clusters that would disappear).
- `entries/<entry id>.json`, one per entry: the frontmatter facts with every evidence pointer resolved to `{label, url}`; `uses` (its links) and `usedBy` (inbound links, with `heading`); `sections` (`title`, `markdown`); `questions` and `dismissed`; `verified` and `stale`; `cluster` (its key), `part` (the database a table counts as, when it is not a part itself), `editUrl`.
- `everything.json`: every entry with `id`, `name`, `kind`, `type`, `summary`, `status`, `cluster`, `unsurveyed`, `verified`, `stale`. The site also serves it, trimmed, for the search box.
- `questions.json`: every open, needs-person and answered question with its entry and `editUrl`.
- `changes.json`: change summaries from `changes/`, newest first: `date`, `repository`, `commit`, `title`, `markdown`.
- `tags.json`: each business-data tag with its `tables` and the `clusters` they are in.

An evidence `url` points at the file and lines at the repository's primary location: `github.com/<org>/<repo>/blob/<commit>/<path>#L..` or the Azure DevOps equivalent.
