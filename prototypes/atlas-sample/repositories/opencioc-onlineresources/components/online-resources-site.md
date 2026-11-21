---
kind: component
type: site
summary: Classic ASP (VBScript) website of CIOC Online Resources; reaches its data through stored procedures in the primary database.
repository: repositories/opencioc-onlineresources
provides: []
system: {id: systems/cioc-online-resources, basis: inferred}
links:
  - type: uses
    to: databases/cioc-primary
    basis: inferred
    source: "opencioc-onlineresources:includes/core/incConnection.asp@7c461be#L28-L77"
  - type: calls
    to: databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_l
    basis: extracted
    source: "opencioc-onlineresources:includes/savedsearch/incSavedSearchList.asp@7c461be#L31"
  - type: calls
    to: databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_l_shared
    basis: extracted
    source: "opencioc-onlineresources:includes/savedsearch/incSavedSearchList.asp@7c461be#L57"
  - type: calls
    to: databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_s
    basis: extracted
    source: "opencioc-onlineresources:includes/savedsearch/incSavedSearchEdit.asp@7c461be#L57"
  - type: calls
    to: databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_u
    basis: extracted
    source: "opencioc-onlineresources:includes/savedsearch/incSavedSearchEdit2.asp@7c461be#L92"
  - type: calls
    to: databases/cioc-primary/routines/dbo/sp_gbl_savedsearch_d
    basis: extracted
    source: "opencioc-onlineresources:includes/savedsearch/incSavedSearch2.asp@7c461be#L77"
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This is the website staff and the public use to search and maintain community information records. Its pages are written in classic ASP, an older Microsoft web technology. So far only the saved-search feature is mapped: a signed-in user can save a search, list their own and shared searches, edit or share one, and delete it. Each of those actions is carried out by a stored procedure in the primary database.

## Technical detail

- 664 `.asp` files at this commit. `Cioc.Web.csproj` (AssemblyName `Cioc.Web`, .NET Framework 4.8) lists the files as content; it has one `Compile` item.
- Database connections are ADO (`ADODB.Connection`). The connection string is not in the ASP code: it is returned at run time by Python embedded in the page (`includes/core/incConnection.asp#L20-L30`, opened at L77). The database name therefore does not appear in this component's code, which is why the `uses` link is inferred.
- Saved-search pages and what they call:

| Page | Include that does the work | Stored procedure |
|---|---|---|
| `savedsearch.asp` | `includes/savedsearch/incSavedSearchList.asp` | `sp_GBL_SavedSearch_l`, `sp_GBL_SavedSearch_l_Shared` |
| `savedsearch_edit.asp` | `includes/savedsearch/incSavedSearchEdit.asp` | `sp_GBL_SavedSearch_s` |
| `savedsearch_edit2.asp` | `includes/savedsearch/incSavedSearchEdit2.asp` | `sp_GBL_SavedSearch_u` |
| `savedsearch2.asp` | `includes/savedsearch/incSavedSearch2.asp` | `sp_GBL_SavedSearch_d` |

- All five calls are explicit `adCmdStoredProc` commands with a literal procedure name.
- The ASP names the saved-search ID parameter `@SRCH_ID` (delete) and `@SRCHID` (update); the procedures declare it as `@SSRCH_ID`.
- `volunteer/savedsearch*.asp` exist too and have not been read.
- COM objects created by literal ProgID in the files read: `ADODB.Command`, `ADODB.Recordset`, `ADODB.Connection` (Microsoft platform, treated as public).

## Open questions

None on this entry. See the repository entry for the question about the Python application, and `databases/cioc-primary` for the database's deployed name.
