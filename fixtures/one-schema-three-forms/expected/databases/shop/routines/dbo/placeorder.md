---
kind: routine
name: dbo.PlaceOrder
type: procedure
summary: Procedure dbo.PlaceOrder in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Stored Procedures/PlaceOrder.sql@1101f8b
  sql-2: acme-shop-migrations:db/007_routines.sql@d9cbb99
links:
  - type: reads
    to: databases/shop/tables/dbo/customers
    how: sql
    basis: extracted
    source: sql#L8
  - type: writes
    to: databases/shop/tables/dbo/orders
    how: sql
    basis: extracted
    source: sql#L11
verified:
  commit: "d9cbb99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @CustomerId | int | in |
| @OrderId | int | out |

Defined in `db/007_routines.sql`, lines 27-41.
<!-- /extracted -->
