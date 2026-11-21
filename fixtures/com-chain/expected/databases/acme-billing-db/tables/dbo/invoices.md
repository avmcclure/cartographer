---
kind: table
name: dbo.Invoices
summary: Table dbo.Invoices in acme-billing-db; not yet described.
database: databases/acme-billing-db/database
sources:
  sql: acme-billing-db:tables/Invoices.sql@7d1ddd0
links:
  - type: references
    to: databases/acme-billing-db/tables/dbo/customers
    name: FK_Invoices_Customers
    basis: extracted
    source: sql#L4
verified:
  commit: "7d1ddd0"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| InvoiceId | int | no | PK |
| CustomerId | int | no | FK |
| IssuedOn | datetime | no |  |
| Total | decimal(18,2) | no |  |
| Paid | decimal(18,2) | no |  |
| ExportedOn | datetime | yes |  |
<!-- /extracted -->
