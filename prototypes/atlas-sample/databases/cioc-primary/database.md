---
kind: database
summary: Primary application database of CIOC Online Resources; one per deployed site, named differently on each (no fixed name in the repository).
sources: ["opencioc-onlineresources:python/sql/core@7c461be"]
instances: []
system: {id: systems/cioc-online-resources, basis: inferred}
links: []
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This is the main database behind the Online Resources website. It holds the records, users, saved searches and settings for one site. Each deployed site has its own copy under its own name, and all copies lean on a second, shared database (`cioc_shared`) for common helpers such as error messages.

## Technical detail

- Source: `python/sql/core/` in `repositories/opencioc-onlineresources`, a Red Gate SQL Source Control folder. 481 table files, 987 stored procedure files, 313 function files, 12 view files. Default collation `Latin1_General_100_CI_AI` (`python/sql/core/RedGateDatabaseInfo.xml#L4`).
- No live instance is listed in the atlas config, so nothing here has been confirmed against a running database.
- Name: the repository does not fix one. The documentation gives `SiteName_2010_11` as an example and describes three databases per installation: this one, `cioc_shared` and `tax_updater` (`opencioc-onlineresources:docs/source/dbconfig.rst@7c461be#L26-L33`). The ID `cioc-primary` was chosen by the scanning agent from the documentation's term "Primary Application Database".
- It does not use a linked server to reach `cioc_shared`: routines call it by three-part name (for example `cioc_shared.dbo.fn_SHR_STP_FormatError`), so both databases must be on the same instance. Those calls are recorded as `calls` links on each routine, not as a `links-to` on this entry.
- Mapped so far: tables `gbl_savedsearch`, `cic_securitylevel_savedsearch`, `naics_example`; routines `sp_gbl_savedsearch_u`, `sp_gbl_savedsearch_d`. See `_generated/databases/cioc-primary/index.md`.

## Open questions

<!-- Alternative shape under review: questions live ONLY here, not in frontmatter. One heading per question; fixed field names; a person answers by replacing the text after "Answer:". -->

### q1

- State: needs-person
- Raised by: agent
- Attempts: 2
- Missing: The name or names this database is deployed under, and on which server instances.
- Waiting on: person
- Answer looks like: A list of instance and database names (for example `SQL01 / SiteName_2010_11`), or an entry for each instance in the atlas config.
- Answer:
