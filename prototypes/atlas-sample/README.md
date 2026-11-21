# Sample atlas (throwaway prototype)

**This is a throwaway prototype for issue #16.** It is a hand-written example of what an atlas could look like on disk, made so the entry format can be reacted to before it is fixed. Nothing here was produced by a tool, and nothing here is meant to be kept.

Everything outside the "Invented for illustration" list below was read from the public repositories at the commits shown, through the GitHub API, on 2026-11-20.

| Repository | Commit read | Default branch |
|---|---|---|
| `serilog/serilog` | `43105b35095337d77f022561a8f2d3db2f9a7937` | `dev` |
| `serilog/serilog-extensions-hosting` | `d69d6f18fa0a303fee085ee3aeb2db66c6c5612b` | `dev` |
| `OpenCIOC/onlineresources` | `7c461be1158ca0137809902f5ed7dd7953980d9c` | `master` |
| `OpenCIOC/ciocshared` | `6b4fdf33cdecc162d6c43472a97750fc1de2b829` | `master` |

The CIOC part covers one feature only: saved searches (four ASP pages, their includes, the procedures they call and the tables those touch), plus one unrelated table chosen because it has a trigger.

## Entries (18)

Lines are total lines in the file; "fm" is how many of them are frontmatter.

| Entry | Lines | fm | Note |
|---|---|---|---|
| `repositories/serilog-serilog/repository.md` | 25 | 4 | Library repository. |
| `repositories/serilog-serilog/components/serilog.md` | 37 | 9 | Library; provides the name `Serilog`. Public packages listed in the body. |
| `repositories/serilog-serilog-extensions-hosting/repository.md` | 25 | 4 | Consumer repository. |
| `repositories/serilog-serilog-extensions-hosting/components/serilog-extensions-hosting.md` | 58 | 32 | One resolved `depends-on`, one unresolved reference with a CLI-raised question, a `dismissed` note, a `branches` section. |
| `repositories/opencioc-onlineresources/repository.md` | 35 | 12 | Agent-raised open question; scan state beside it. |
| `repositories/opencioc-onlineresources/components/online-resources-site.md` | 60 | 32 | Classic ASP site; `calls` links to five procedures, inferred `uses` link to the database. |
| `repositories/opencioc-ciocshared/repository.md` | 25 | 4 | A repository with no components, only a database source. |
| `databases/cioc-primary/database.md` | 35 | 7 | **Questions in the body only** (the alternative shape). |
| `databases/cioc-primary/tables/dbo/gbl_savedsearch.md` | 56 | 6 | Two foreign keys; **the person-owned passage**. |
| `databases/cioc-primary/tables/dbo/cic_securitylevel_savedsearch.md` | 42 | 6 | Link table with two foreign keys. |
| `databases/cioc-primary/tables/dbo/naics_example.md` | 50 | 6 | Table with a trigger that writes another table. |
| `databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_u.md` | 85 | 49 | Eleven links, three of them into `cioc_shared` (cross-repository). The heaviest frontmatter. |
| `databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_d.md` | 61 | 33 | Seven links, three cross-repository. |
| `databases/cioc_shared/database.md` | 33 | 15 | Question in frontmatter, needing a person. |
| `databases/cioc_shared/tables/dbo/shr_stp_error.md` | 40 | 6 | One foreign key. |
| `databases/cioc_shared/routines/dbo/sp_stp_unknownerrorcheck.md` | 35 | 9 | Target of the cross-repository calls. |
| `databases/cioc_shared/routines/dbo/fn_shr_stp_formaterror.md` | 38 | 13 | Function that reads a table. |
| `systems/cioc-online-resources.md` | 34 | 17 | Agent-proposed grouping; **the `basis: stated` field** (`owner`). |

Other files:

| File | Lines | Note |
|---|---|---|
| `AGENTS.md` | 25 | How an agent navigates. |
| `_generated/index.md` | 38 | Root index. |
| `_generated/databases/cioc-primary/index.md` | 14 | One folder index. `repositories/index.md` and `databases/index.md` are named in the root index but not written. |
| `_generated/databases/cioc-primary/tables/dbo/gbl_savedsearch.used-by.md` | 26 | Reverse list for a table (read by, written by). |
| `_generated/databases/cioc_shared/routines/dbo/sp_stp_unknownerrorcheck.used-by.md` | 12 | Reverse list for a routine, with callers from another repository. |
| `_generated/open-questions.md` | 25 | Atlas-wide question list. |
| `repositories/opencioc-onlineresources/scan-state.yml` | 24 | Scan state, showing an interrupted scan. |

Average entry: 43 lines. Largest: 85 lines, of which 49 are frontmatter.

### Link targets that have no file here

The sample is a slice, so these IDs are linked to (truthfully) but not written: tables `gbl_users`, `cic_securitylevel`, `vol_securitylevel`, `vol_securitylevel_savedsearch`; routines `sp_gbl_savedsearch_l`, `sp_gbl_savedsearch_l_shared`, `sp_gbl_savedsearch_s`, `fn_gbl_parseintidlist` (all in `cioc-primary`); routines `fn_shr_gbl_sprintf`, `fn_shr_stp_objectname` (in `cioc_shared`). The reverse lists only show links from entries that exist in the sample.

## Invented for illustration

Everything else is from files that were read.

1. **The atlas config.** There is no config file in the sample. The entries pretend it lists `Serilog.*` as an internal name prefix (which is why `Serilog.Extensions.Logging` is an unresolved reference and not a public package), `release/*` as the branch pattern, and no live database instances.
2. **The release branch.** `release/10.0` on `serilog-extensions-hosting`, and its `Serilog 4.3.0` difference, do not exist. The real repository has only `dev` and `main`, with identical package references.
3. **The person-owned passage** in `gbl_savedsearch.md` (between the `<!-- person -->` markers). No person wrote it and its claim about `UpgradeVerified` is not verified.
4. **The stated field**: `owner: "CIOC platform team"` on the system entry.
5. **Dates, the tool version and attempt counts**: `2026-11-20`, `0.0.0-prototype`, and the `attempts` numbers on questions.
6. **The database ID `cioc-primary`.** The real database has a different name on every site; see question 9 below.

## Additions to the proposed shapes

Three things were needed that the proposed frontmatter did not have. They are in the sample and need a verdict:

- `sources:` on database, table and routine entries: the file or folder the entry was built from, in the same pointer format. Without it a table entry has nowhere structured to say where it came from, and `verified.commit` does not say which repository the commit belongs to.
- `instances: []` on database entries, for the server instances it is known on (#8) and, later, the permission level of each read (#12).
- `scan-state.yml` as the name of the scan state file.

## Questions for the reviewer

Format choices:

1. **Source as one compact string, or structured?** `"opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L114"` is short, but real paths contain spaces, and could contain `@`, `#` or `:`. A structured form (`{repository, path, commit, lines}`) is unambiguous and three lines longer per link.
2. **The same file is repeated on every link.** `sp_gbl_savedsearch_u` has eleven links and all eleven sources differ only in line numbers; that is most of its 49 frontmatter lines. Should links from the entry's own source file carry only `lines:`, with the file taken from `sources`?
3. **Questions in frontmatter or in the body?** Compare `databases/cioc_shared/database.md` (frontmatter, plus a prose rendering in the body: the same text twice) with `databases/cioc-primary/database.md` (body only, fixed field names, an `Answer:` line). The frontmatter form has no obvious place for a person to write an answer.
4. **How heavy is the frontmatter?** Entries with few links are fine (4 to 9 lines). Routines and the consumer component are more than half frontmatter. Is that acceptable for a file people are also meant to read on GitHub?
5. **No `name` or title.** The ID is lower-cased, so the real name (`dbo.GBL_SavedSearch`, `Serilog.Extensions.Hosting`) appears only inside `summary`. Should there be a `name:` key, or a top heading?
6. **`system:` on the member and `contains` on the system say the same thing twice.** #8 says links are stored once, on the entry they start from. Which side is authoritative? Also, `system:` is singular, and `cioc_shared` is by its own README shared by several applications.
7. **Tags, `system` and `provides` carry a basis but no source**, although #12 says every structured field carries its own source. Add one, or relax the rule for these?
8. **Where do public packages go?** #8 says they are listed on the component that uses them. They are in a body table here because the frontmatter has no place for them. Is that enough, or should they be queryable?

Things that were awkward on real code:

9. **A database with no fixed name.** #8 identifies a database by name, but the Online Resources primary database is named per site (the docs use `SiteName_2010_11` as an example) and the name never appears in the code: the ASP gets its connection string at run time from embedded Python. The ID had to be made up. What should a database's ID be when the repository does not name it?
10. **The name `cioc_shared` is itself inferred.** The `ciocshared` repository does not state its database's name; it comes from three-part names in the other repository's SQL and from that repository's docs. The cross-repository links depend on this inference, and there is nowhere to record its basis.
11. **Triggers write data, and nothing shows it.** `tr_NAICS_Example_iud` updates `NAICS_Description`, but triggers are not entries and tables do not have `writes` links, so this flow is invisible to anything that walks links. Same for foreign keys: they are facts in a body table, so "which tables reference this one" cannot be generated without parsing the body.
12. **Repository locations have no frontmatter key.** They are in a body table and in the scan state. Given the Azure DevOps to GitHub move, should `locations` be structured on the repository entry?
13. **A repository with no components** (`opencioc-ciocshared`) links to nothing; its relation to the database it is the source for exists only as `sources` on the database side. Is that enough?
14. **One component or two?** `onlineresources` holds a classic ASP site and a Python application that share sessions and a reverse proxy. The sample writes one component and raises a question. Is "one deployable site" or "one technology" the unit?
15. **`dismissed` used for an agent's own non-question.** The PolySharp note records a conclusion ("build-time only") so it is not re-asked. Is that the intended use, or is `dismissed` only for questions that once existed?
16. **Unquoted dates.** `date: 2026-11-20` is read by YAML parsers as a date, not a string. Quote them, or accept typed dates?
17. **Empty sections.** Most entries end with `## Open questions` / `None.` and most have `links: []`. Keep the fixed sections even when empty?
18. **Reverse lists and indirection.** The table's used-by file adds a "reached indirectly (one step)" block showing the site reaches the table through the procedures. Is that wanted in the generated file, or only from the CLI's impact command?
