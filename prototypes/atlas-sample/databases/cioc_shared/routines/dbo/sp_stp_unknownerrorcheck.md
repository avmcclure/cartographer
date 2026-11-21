---
kind: routine
summary: dbo.sp_STP_UnknownErrorCheck; turns a non-zero SQL error number into a standard "unknown error" message for the caller.
sources: ["opencioc-ciocshared:Stored Procedures/dbo.sp_STP_UnknownErrorCheck.sql@6b4fdf3"]
links:
  - type: calls
    to: databases/cioc_shared/routines/dbo/fn_shr_stp_formaterror
    basis: extracted
    source: "opencioc-ciocshared:Stored Procedures/dbo.sp_STP_UnknownErrorCheck.sql@6b4fdf3#L14"
verified: {commit: 6b4fdf3, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

A small shared helper. Procedures in application databases call it straight after changing data, passing the database's error number. If the number shows a failure, the helper produces a readable message naming the thing that was being changed; otherwise it reports success. It touches no tables itself.

## Technical detail

Stored procedure, `EXECUTE AS CALLER`. Execute granted to `cioc_login_role` and `cioc_maintenance_role`.

| Parameter | Type | Direction | Notes |
|---|---|---|---|
| @Error | int | in | usually `@@ERROR` from the caller |
| @ObjectName | nvarchar(50) | in | display name of the thing being changed |
| @ErrMsg | nvarchar(500) | out | set only when `@Error` is not 0 |

Returns 1 and sets `@ErrMsg` to `fn_SHR_STP_FormatError(1, @ObjectName, @Error)` when `@Error` is not 0; returns 0 otherwise.

Called across databases by three-part name, `cioc_shared.dbo.sp_STP_UnknownErrorCheck`. Callers: `_generated/databases/cioc_shared/routines/dbo/sp_stp_unknownerrorcheck.used-by.md`.

Source is not copied here; read it at the path in `sources` (25 lines).

## Open questions

None.
