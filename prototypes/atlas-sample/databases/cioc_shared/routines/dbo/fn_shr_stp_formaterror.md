---
kind: routine
summary: dbo.fn_SHR_STP_FormatError; returns the message text for an error number in the session's language, with up to two values filled in.
sources: ["opencioc-ciocshared:Functions/dbo.fn_SHR_STP_FormatError.sql@6b4fdf3"]
links:
  - type: reads
    to: databases/cioc_shared/tables/dbo/shr_stp_error
    basis: extracted
    source: "opencioc-ciocshared:Functions/dbo.fn_SHR_STP_FormatError.sql@6b4fdf3#L19-L22"
  - type: calls
    to: databases/cioc_shared/routines/dbo/fn_shr_gbl_sprintf
    basis: extracted
    source: "opencioc-ciocshared:Functions/dbo.fn_SHR_STP_FormatError.sql@6b4fdf3#L24"
verified: {commit: 6b4fdf3, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

Application procedures report problems as numbers. This function turns a number into the sentence a user sees, choosing the wording in the session's language when one exists and falling back to another language when it does not. Up to two values, such as a record ID and the kind of record, are inserted into the sentence.

## Technical detail

Scalar function, returns `nvarchar(1000)`, `EXECUTE AS CALLER`. Execute granted to `cioc_login_role`.

| Parameter | Type | Notes |
|---|---|---|
| @Error | int | error number; key into `SHR_STP_Error` |
| @ErrParam1 | nvarchar(255) | first value to insert; may be null |
| @ErrParam2 | nvarchar(255) | second value to insert; may be null |

- Picks the row of `SHR_STP_Error` for `@Error` whose `LangID` equals `@@LANGID`, else the lowest `LangID` available.
- Joins the two values with the separator `$,$` and passes template, values and separator to `fn_SHR_GBL_sprintf`.

Source is not copied here; read it at the path in `sources` (41 lines).

## Open questions

None.
