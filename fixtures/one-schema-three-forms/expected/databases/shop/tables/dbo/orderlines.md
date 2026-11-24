---
kind: table
name: dbo.OrderLines
summary: Table dbo.OrderLines in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Tables/OrderLines.sql@1101f8b
  sql-2: acme-shop-migrations:db/003_orders.sql@d9cbb99
  sql-2-2: acme-shop-migrations:db/005_order_lines_product_key.sql@d9cbb99
links:
  - type: references
    to: databases/shop/tables/dbo/orders
    name: FK_OrderLines_Orders
    how: sql
    basis: extracted
    source: sql#L8
  - type: references
    to: databases/shop/tables/dbo/products
    name: FK_OrderLines_Products
    how: sql
    basis: extracted
    source: sql#L9
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
| OrderId | int | no | FK |
| ProductId | int | no | FK |
| Quantity | int | no |  |
| UnitPrice | decimal(10,2) | no |  |
<!-- /extracted -->
