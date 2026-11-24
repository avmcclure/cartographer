---
kind: routine
name: dbo.usp_GetInvoice
type: procedure
summary: Procedure dbo.usp_GetInvoice in acme-billing-db; not yet described.
database: databases/acme-billing-db/database
sources:
  sql: acme-billing-db:procedures/usp_GetInvoice.sql@7d1ddd0
links:
  - type: reads
    to: databases/acme-billing-db/tables/dbo/invoices
    how: sql
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
| @InvoiceId | int | in |

Defined in `procedures/usp_GetInvoice.sql`, lines 1-9.
<!-- /extracted -->
