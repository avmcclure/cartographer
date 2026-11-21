---
kind: routine
summary: dbo.sp_GBL_SavedSearch_d; deletes one of the calling user's saved searches together with its sharing rows.
sources: ["opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be"]
links:
  - type: reads
    to: databases/cioc-primary/tables/dbo/gbl_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L33-L36"
  - type: writes
    to: databases/cioc-primary/tables/dbo/cic_securitylevel_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L40-L41"
  - type: writes
    to: databases/cioc-primary/tables/dbo/vol_securitylevel_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L42-L43"
  - type: writes
    to: databases/cioc-primary/tables/dbo/gbl_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L44"
  - type: calls
    to: databases/cioc_shared/routines/dbo/fn_shr_stp_objectname
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L26"
  - type: calls
    to: databases/cioc_shared/routines/dbo/fn_shr_stp_formaterror
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L31"
  - type: calls
    to: databases/cioc_shared/routines/dbo/sp_stp_unknownerrorcheck
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_d.sql@7c461be#L45"
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This procedure runs when a user deletes a saved search. It refuses unless the search exists and belongs to that user. It then removes the records of who the search was shared with, and finally the search itself.

## Technical detail

Stored procedure, `EXECUTE AS CALLER`. Execute granted to `cioc_login_role`.

| Parameter | Type | Direction | Notes |
|---|---|---|---|
| @User_ID | int | in | must own the search |
| @SSRCH_ID | int | in | |
| @ErrMsg | nvarchar(500) | out | |

Returns 0 on success, otherwise an error number: 2 (no ID given), 3 (no such record), 8 (security failure).

- Deletes from `CIC_SecurityLevel_SavedSearch` and `VOL_SecurityLevel_SavedSearch` first, then from `GBL_SavedSearch`. The three deletes are not wrapped in an explicit transaction in this procedure.
- Only the last delete is followed by the unknown-error check.
- Called from the site at `opencioc-onlineresources:includes/savedsearch/incSavedSearch2.asp@7c461be#L77`.

Source is not copied here; read it at the path in `sources` (55 lines).

## Open questions

None.
