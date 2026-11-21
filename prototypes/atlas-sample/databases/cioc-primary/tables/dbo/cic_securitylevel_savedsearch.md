---
kind: table
summary: dbo.CIC_SecurityLevel_SavedSearch; link table recording which CIC security levels a saved search is shared with.
sources: ["opencioc-onlineresources:python/sql/core/Tables/dbo.CIC_SecurityLevel_SavedSearch.sql@7c461be"]
tags: [{value: access-control, basis: inferred}]
links: []
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

When a user shares a saved search, this table records who it is shared with. Sharing is by security level (a group of users with the same rights), not by individual. Each row pairs one saved search with one security level. A matching table, `VOL_SecurityLevel_SavedSearch`, does the same for the volunteer side of the product.

## Technical detail

| Column | Type | Null | Notes |
|---|---|---|---|
| SL_SRCH_ID | int identity | no | primary key |
| SL_ID | int | no | security level |
| SSRCH_ID | int | no | saved search |

Keys and constraints

| Name | Kind | Detail |
|---|---|---|
| PK_CIC_SecurityLevel_SRCH | primary key, clustered | SL_SRCH_ID |
| IX_CIC_SecurityLevel_SavedSearch_UniquePair | unique index | SL_ID, SSRCH_ID |

Foreign keys

| Name | Columns | References | On delete / update |
|---|---|---|---|
| FK_CIC_SecurityLevel_SavedSearch_GBL_SavedSearch | SSRCH_ID | `databases/cioc-primary/tables/dbo/gbl_savedsearch` (SSRCH_ID) | none |
| FK_CIC_SecurityLevel_SavedSearch_CIC_SecurityLevel | SL_ID | `databases/cioc-primary/tables/dbo/cic_securitylevel` (SL_ID) | cascade / cascade; not trusted |

Triggers: none.

Because the foreign key to `GBL_SavedSearch` does not cascade, rows here must be deleted before the saved search; `sp_GBL_SavedSearch_d` does so.

## Open questions

None.
