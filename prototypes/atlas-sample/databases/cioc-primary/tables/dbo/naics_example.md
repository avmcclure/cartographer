---
kind: table
summary: dbo.NAICS_Example; example activities for an industry (NAICS) code, per language. A trigger keeps a combined text in NAICS_Description.
sources: ["opencioc-onlineresources:python/sql/core/Tables/dbo.NAICS_Example.sql@7c461be"]
tags: [{value: reference-data, basis: inferred}]
links: []
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

Industry classification codes come with example activities, one row per example and language. Whenever examples are added, changed or removed, the database itself refreshes a ready-made combined examples text stored on the code's description row. This table is outside the saved-search feature; it is included because it has a trigger.

## Technical detail

| Column | Type | Null | Notes |
|---|---|---|---|
| Example_ID | int identity | no | primary key |
| CREATED_DATE | smalldatetime | yes | |
| CREATED_BY | varchar(50) | yes | |
| MODIFIED_DATE | smalldatetime | yes | |
| MODIFIED_BY | varchar(50) | yes | |
| Code | varchar(6) | no | NAICS code |
| LangID | smallint | no | |
| Description | nvarchar(255) | no | |

Keys and constraints

| Name | Kind | Detail |
|---|---|---|
| PK_NAICS_Example | primary key, clustered | Example_ID |

Foreign keys

| Name | Columns | References | On delete / update |
|---|---|---|---|
| FK_NAICS_Example_NAICS | Code | `databases/cioc-primary/tables/dbo/naics` (Code) | cascade / cascade; not trusted |
| FK_NAICS_Example_STP_Language | LangID | `databases/cioc-primary/tables/dbo/stp_language` (LangID) | none |

Triggers

| Name | Fires on | What it does |
|---|---|---|
| tr_NAICS_Example_iud | insert, update, delete | Sets `CMP_Examples` on the matching rows of `databases/cioc-primary/tables/dbo/naics_description` to the result of `dbo.fn_NAICS_FullExamples(Code, LangID)`. Defined at L17-L31 of the source file. |

Permissions in source: `SELECT` granted to `cioc_cic_search_role`.

## Open questions

None.
