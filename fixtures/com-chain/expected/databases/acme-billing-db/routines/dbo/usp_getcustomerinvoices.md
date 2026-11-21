---
kind: routine
name: dbo.usp_GetCustomerInvoices
type: procedure
summary: Procedure dbo.usp_GetCustomerInvoices in acme-billing-db; not yet described.
database: databases/acme-billing-db/database
sources:
  sql: acme-billing-db:procedures/usp_GetCustomerInvoices.sql@7d1ddd0
links:
  - type: reads
    to: databases/acme-billing-db/tables/dbo/customers
    basis: extracted
    source: sql#L8
  - type: reads
    to: databases/acme-billing-db/tables/dbo/invoices
    basis: extracted
    source: sql#L7
verified:
  commit: "7d1ddd0"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @CustomerId | int | in |

Defined in `procedures/usp_GetCustomerInvoices.sql`, lines 1-11.
<!-- /extracted -->
