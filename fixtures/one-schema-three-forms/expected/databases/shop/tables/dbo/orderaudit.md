---
kind: table
name: dbo.OrderAudit
summary: Table dbo.OrderAudit in Shop; not yet described.
database: databases/shop/database
sources:
  sql: acme-shop-database:Shop/dbo/Tables/OrderAudit.sql@1101f8b
  sql-2: acme-shop-migrations:db/006_order_audit.sql@d9cbb99
verified:
  commit: "d9cbb99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| Id | bigint | no | PK |
| OrderId | int | no |  |
| Status | varchar(20) | no |  |
| ChangedAt | datetime2(0) | no |  |
<!-- /extracted -->
