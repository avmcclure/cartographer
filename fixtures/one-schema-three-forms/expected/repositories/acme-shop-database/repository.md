---
kind: repository
name: acme-shop-database
summary: Repository acme-shop-database; not yet described.
stack:
  - sql
sources:
  schema: acme-shop-database:Ledger/Ledger.sqlproj@1101f8b
  schema-2: acme-shop-database:Shop/Shop.sqlproj@1101f8b
links:
  - type: defines
    to: databases/ledger/database
    basis: extracted
    source: schema
  - type: defines
    to: databases/shop/database
    basis: extracted
    source: schema-2
verified:
  commit: "1101f8b"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Files: 14. Most common: `.sql` 12, `.sqlproj` 2.
<!-- /extracted -->
