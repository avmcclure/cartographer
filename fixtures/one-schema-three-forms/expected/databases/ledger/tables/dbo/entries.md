---
kind: table
name: dbo.Entries
summary: Table dbo.Entries in Ledger; not yet described.
database: databases/ledger/database
sources:
  sql: acme-shop-database:Ledger/dbo/Tables/Entries.sql@1101f8b
verified:
  commit: "1101f8b"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| Id | bigint | no | PK |
| Reference | int | no |  |
| Amount | decimal(12,2) | no |  |
| PostedAt | datetime2(0) | no |  |
<!-- /extracted -->
