---
kind: routine
summary: dbo.sp_GBL_SavedSearch_u; creates or updates a user's saved search and sets which security levels it is shared with.
sources: ["opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be"]
links:
  - type: reads
    to: databases/cioc-primary/tables/dbo/gbl_users
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L41-L44"
  - type: reads
    to: databases/cioc-primary/tables/dbo/gbl_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L55"
  - type: writes
    to: databases/cioc-primary/tables/dbo/gbl_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L92-L123"
  - type: reads
    to: databases/cioc-primary/tables/dbo/cic_securitylevel
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L133"
  - type: reads
    to: databases/cioc-primary/tables/dbo/vol_securitylevel
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L134"
  - type: writes
    to: databases/cioc-primary/tables/dbo/cic_securitylevel_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L137-L149"
  - type: writes
    to: databases/cioc-primary/tables/dbo/vol_securitylevel_savedsearch
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L151-L164"
  - type: calls
    to: databases/cioc-primary/routines/dbo/fn_gbl_parseintidlist
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L132"
  - type: calls
    to: databases/cioc_shared/routines/dbo/fn_shr_stp_objectname
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L34-L36"
  - type: calls
    to: databases/cioc_shared/routines/dbo/fn_shr_stp_formaterror
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L54"
  - type: calls
    to: databases/cioc_shared/routines/dbo/sp_stp_unknownerrorcheck
    basis: extracted
    source: "opencioc-onlineresources:python/sql/core/Stored Procedures/dbo.sp_GBL_SavedSearch_u.sql@7c461be#L114"
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This procedure runs when a user saves a new search or edits an existing one. It checks that the request is valid (the search has a name, the name is not already used by that user, the user is within their quota and owns the search being edited), then stores the search and updates the list of security levels it is shared with. If anything is wrong it returns an error number and a readable message built by the shared database.

## Technical detail

Stored procedure, `EXECUTE AS CALLER`. Execute granted to `cioc_login_role`.

| Parameter | Type | Direction | Notes |
|---|---|---|---|
| @SSRCH_ID | int | in/out | null to create; returns the new ID |
| @User_ID | int | in | |
| @SearchName | varchar(255) | in | trimmed; required |
| @WhereClause | nvarchar(max) | in | required |
| @Notes | nvarchar(2000) | in | trimmed |
| @IncludeDeleted | bit | in | |
| @Domain | tinyint | in | 1 or 2 |
| @SharedWithSLs | varchar(max) | in | comma-separated security level IDs |
| @ErrMsg | nvarchar(500) | out | |

Returns 0 on success, otherwise an error number: 23 (no module), 3 (no such record), 8 (security failure), 10 (required field), 6 (value in use), 17 (over saved-search quota).

- Reads the user's `MemberID_Cache` and `SavedSearchQuota` from `GBL_Users`.
- Inserts into or updates `GBL_SavedSearch`. Uses `@@LANGID` as the row's language.
- For `@Domain` 1 it replaces the sharing rows in `CIC_SecurityLevel_SavedSearch`; for 2, in `VOL_SecurityLevel_SavedSearch`. Only security levels belonging to the user's member are accepted.
- Calls into `cioc_shared` by three-part name for object names, error text and the unknown-error check (the same call appears on several more lines than the one cited on each link).
- Called from the site at `opencioc-onlineresources:includes/savedsearch/incSavedSearchEdit2.asp@7c461be#L92`.

Source is not copied here; read it at the path in `sources` (177 lines).

## Open questions

None.
