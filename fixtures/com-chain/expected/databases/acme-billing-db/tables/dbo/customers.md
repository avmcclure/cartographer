---
kind: table
name: dbo.Customers
summary: Table dbo.Customers in acme-billing-db; not yet described.
database: databases/acme-billing-db/database
sources:
  sql: acme-billing-db:tables/Customers.sql@7d1ddd0
verified:
  commit: "7d1ddd0"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| CustomerId | int | no | PK |
| Name | nvarchar(200) | no |  |
| Email | nvarchar(320) | yes |  |
<!-- /extracted -->
