---
kind: routine
name: dbo.OrderTotal
type: function
summary: Function dbo.OrderTotal in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Functions/OrderTotal.sql@1101f8b
  sql-2: acme-shop-migrations:db/007_routines.sql@d9cbb99
links:
  - type: reads
    to: databases/shop/tables/dbo/orderlines
    basis: extracted
    source: sql#L7
verified:
  commit: "d9cbb99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @OrderId | int | in |

Defined in `db/007_routines.sql`, lines 4-13.
<!-- /extracted -->
