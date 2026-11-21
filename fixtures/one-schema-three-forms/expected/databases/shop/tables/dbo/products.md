---
kind: table
name: dbo.Products
summary: Table dbo.Products in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Tables/Products.sql@1101f8b
  sql-2: acme-shop-migrations:db/002_customers_and_products.sql@d9cbb99
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
| Sku | varchar(20) | no |  |
| Name | nvarchar(100) | no |  |
| Price | decimal(10,2) | no |  |
<!-- /extracted -->
