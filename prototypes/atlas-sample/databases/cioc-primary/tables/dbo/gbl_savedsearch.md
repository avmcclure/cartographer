---
kind: table
summary: dbo.GBL_SavedSearch; one row per search a user has saved, including the search's SQL WHERE clause.
sources: ["opencioc-onlineresources:python/sql/core/Tables/dbo.GBL_SavedSearch.sql@7c461be"]
tags: [{value: user-content, basis: inferred}]
links: []
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

Each row is a search that a signed-in user chose to keep, with a name, optional notes and the search conditions themselves. A saved search belongs to one user and to one of two areas of the product (the `Domain` column, limited to 1 or 2). It can be shared with groups of users through two companion tables.

<!-- person -->
Saved searches are the main reason old column names cannot be dropped casually: the stored conditions are raw SQL and are not rewritten when the schema changes. Check `UpgradeVerified` before assuming a saved search still runs.
<!-- /person -->

## Technical detail

| Column | Type | Null | Notes |
|---|---|---|---|
| SSRCH_ID | int identity | no | primary key |
| CREATED_DATE | smalldatetime | yes | |
| MODIFIED_DATE | smalldatetime | yes | |
| User_ID | int | no | owner of the search |
| SearchName | nvarchar(255) | no | |
| Domain | tinyint | no | check: 1 or 2 |
| LangID | smallint | no | default 0 |
| WhereClause | nvarchar(max) | no | SQL condition text |
| IncludeDeleted | bit | no | default 0 |
| Notes | nvarchar(max) | yes | |
| UpgradeVerified | bit | no | default 0 |

Keys and constraints

| Name | Kind | Detail |
|---|---|---|
| PK_GBL_SavedSearch | primary key, clustered | SSRCH_ID |
| CK_GBL_SavedSearch | check (not trusted) | Domain > 0 and Domain <= 2 |

Foreign keys

| Name | Columns | References | On delete / update |
|---|---|---|---|
| FK_GBL_SavedSearch_GBL_Users | User_ID | `databases/cioc-primary/tables/dbo/gbl_users` (User_ID) | cascade / cascade; not trusted |
| FK_GBL_SavedSearch_STP_Language | LangID | `databases/cioc-primary/tables/dbo/stp_language` (LangID) | none |

Triggers: none.

Permissions in source: `SELECT` granted to `cioc_login_role`.

Read by, written by: `_generated/databases/cioc-primary/tables/dbo/gbl_savedsearch.used-by.md`.

## Open questions

None.
