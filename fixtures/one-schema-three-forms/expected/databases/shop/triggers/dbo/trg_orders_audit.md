---
kind: trigger
name: dbo.trg_Orders_Audit
summary: Trigger dbo.trg_Orders_Audit on dbo.Orders in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Triggers/trg_Orders_Audit.sql@1101f8b
  sql-2: acme-shop-migrations:db/006_order_audit.sql@d9cbb99
links:
  - type: writes
    to: databases/shop/tables/dbo/orderaudit
    basis: extracted
    source: sql#L9
  - type: fires-on
    to: databases/shop/tables/dbo/orders
    basis: extracted
    source: sql#L2
verified:
  commit: "d9cbb99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
Fires after insert, update on dbo.Orders.

Defined in `db/006_order_audit.sql`, lines 14-26.
<!-- /extracted -->
