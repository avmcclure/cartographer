---
kind: system
summary: CIOC Online Resources; the community-information website with its primary database and the shared library database it needs.
owner: {value: "CIOC platform team", basis: stated, source: "person:2026-11-20"}
links:
  - type: contains
    to: repositories/opencioc-onlineresources/components/online-resources-site
    basis: inferred
    source: "opencioc-onlineresources:docs/source/intro.rst@7c461be#L20-L44"
  - type: contains
    to: databases/cioc-primary
    basis: inferred
    source: "opencioc-onlineresources:docs/source/dbconfig.rst@7c461be#L26-L28"
  - type: contains
    to: databases/cioc_shared
    basis: inferred
    source: "opencioc-onlineresources:docs/source/dbconfig.rst@7c461be#L30-L37"
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

CIOC Online Resources is the product community information centres use to publish, search and maintain records of organisations, services and volunteer opportunities. As mapped so far it is made of one website and two databases: the site's own primary database and a shared library database. The grouping was proposed by the scanning agent from the product's own documentation; a person has not confirmed it.

## Technical detail

- The site calls stored procedures in the primary database; those procedures call helper routines in `cioc_shared` by three-part name. Example path: `savedsearch_edit2.asp` → `sp_GBL_SavedSearch_u` → `cioc_shared.dbo.sp_STP_UnknownErrorCheck` → `fn_SHR_STP_FormatError` → table `SHR_STP_Error`.
- The documentation says `cioc_shared` is required to run the software and is shared with all databases on the same server (`opencioc-onlineresources:docs/source/dbconfig.rst@7c461be#L35-L37`). It is placed in this system because this is the only consumer the atlas knows; see the open question on `databases/cioc_shared`.
- A third database, `tax_updater`, is named in the same document and has no entry: no source for it has been scanned.
- Members come from two repositories: `repositories/opencioc-onlineresources` and `repositories/opencioc-ciocshared`.

## Open questions

None on this entry.
