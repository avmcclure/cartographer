---
kind: routine
name: dbo.PostEntry
type: procedure
summary: Procedure dbo.PostEntry in Ledger; not yet described.
database: databases/ledger/database
sources:
  sql: acme-shop-database:Ledger/dbo/Stored Procedures/PostEntry.sql@1101f8b
links:
  - type: writes
    to: databases/ledger/tables/dbo/entries
    how: sql
    basis: extracted
    source: sql#L8
verified:
  commit: "1101f8b"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @Reference | int | in |
| @Amount | decimal(12,2) | in |

Defined in `Ledger/dbo/Stored Procedures/PostEntry.sql`, lines 1-10.
<!-- /extracted -->
