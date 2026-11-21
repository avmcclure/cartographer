---
kind: repository
summary: Source of the cioc_shared database; SQL functions, procedures and reference data shared by several CIOC applications.
links: []
verified: {commit: 6b4fdf3, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This repository holds a small database of shared helpers: text formatting, error messages, translated names and similar building blocks. It ships no application of its own. Its README says the contents are shared by several CIOC applications, including the Online Resources software.

## Technical detail

| Location | Identifier | Primary | Default branch |
|---|---|---|---|
| GitHub | `OpenCIOC/ciocshared` | yes | `master` |

- Components: none. The repository is only a database source.
- Database source: the repository root is the source for [`databases/cioc_shared`](../../databases/cioc_shared/database.md). It is a Red Gate SQL Source Control folder (`RedGate.ssc`) with 9 table files, 2 stored procedure files, 73 function files, 7 static data files and one CLR assembly script (`Assemblies/Msdn.SqlRegex.sql`).
- Stated purpose: `opencioc-ciocshared:README.md@6b4fdf3#L4-L6`.
- Tracked branches: `master` only. No branch matches `release/*`.

## Open questions

None on the repository itself. The database entry has one.
