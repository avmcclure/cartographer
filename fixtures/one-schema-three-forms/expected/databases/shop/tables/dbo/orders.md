---
kind: table
name: dbo.Orders
summary: Table dbo.Orders in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Tables/Orders.sql@1101f8b
  sql-2: acme-shop-migrations:db/003_orders.sql@d9cbb99
links:
  - type: references
    to: databases/shop/tables/dbo/customers
    name: FK_Orders_Customers
    basis: extracted
    source: sql#L7
verified:
  commit: "d9cbb99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| Id | int | no | PK |
| CustomerId | int | no | FK |
| PlacedAt | datetime2(0) | no |  |
| Status | varchar(20) | no |  |
<!-- /extracted -->
