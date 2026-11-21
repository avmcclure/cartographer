---
kind: view
name: dbo.OrderSummary
summary: View dbo.OrderSummary in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Views/OrderSummary.sql@1101f8b
  sql-2: acme-shop-migrations:db/007_routines.sql@d9cbb99
links:
  - type: calls
    to: databases/shop/routines/dbo/ordertotal
    how: sql
    basis: extracted
    source: sql#L7
  - type: reads
    to: databases/shop/tables/dbo/customers
    basis: extracted
    source: sql#L9
  - type: reads
    to: databases/shop/tables/dbo/orders
    basis: extracted
    source: sql#L8
verified:
  commit: "d9cbb99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
Defined in `db/007_routines.sql`, lines 16-24.
<!-- /extracted -->
