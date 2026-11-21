---
kind: table
summary: dbo.SHR_STP_Error; the text of each numbered application error message, per language.
sources: ["opencioc-ciocshared:Tables/dbo.SHR_STP_Error.sql@6b4fdf3"]
tags: [{value: reference-data, basis: inferred}]
links: []
verified: {commit: 6b4fdf3, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

Stored procedures across the CIOC databases report problems as error numbers. This table holds the wording for each number in each language, with placeholders that are filled in when the message is built. It is reference data that ships with the software rather than data entered by users.

## Technical detail

| Column | Type | Null | Notes |
|---|---|---|---|
| Error | int | no | primary key, part 1 |
| LangID | smallint | no | primary key, part 2 |
| Description | nvarchar(255) | no | message template; case-sensitive collation |

Keys and constraints

| Name | Kind | Detail |
|---|---|---|
| PK_SHR_STP_Error | primary key, clustered | Error, LangID |

Foreign keys

| Name | Columns | References | On delete / update |
|---|---|---|---|
| FK_SHR_STP_Error_SHR_STP_Language | LangID | `databases/cioc_shared/tables/dbo/shr_stp_language` (LangID) | none |

Triggers: none.

Rows are shipped as static data in `opencioc-ciocshared:Data/dbo.SHR_STP_Error_Data.sql@6b4fdf3` (not read for this entry).

## Open questions

None.
