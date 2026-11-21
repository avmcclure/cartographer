---
kind: routine
name: dbo.CompleteOrder
type: procedure
summary: Procedure dbo.CompleteOrder in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Stored Procedures/CompleteOrder.sql@1101f8b
  sql-2: acme-shop-migrations:db/007_routines.sql@d9cbb99
links:
  - type: calls
    to: databases/ledger/routines/dbo/postentry
    name: dbo.PostEntry
    how: sql
    basis: extracted
    source: sql#L18
  - type: calls
    to: databases/shop/routines/dbo/ordertotal
    how: sql
    basis: extracted
    source: sql#L8
  - type: reads
    to: databases/shop/tables/dbo/orderlines
    basis: extracted
    source: sql#L15
  - type: writes
    to: databases/shop/tables/dbo/orders
    basis: extracted
    source: sql#L10
  - type: writes
    name: dbo.StockLevels
    database: Warehouse
    basis: extracted
    source: sql#L14
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

Defined in `db/007_routines.sql`, lines 45-62.
<!-- /extracted -->
