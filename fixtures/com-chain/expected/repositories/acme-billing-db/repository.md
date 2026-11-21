---
kind: repository
name: acme-billing-db
summary: Repository acme-billing-db; not yet described.
stack:
  - sql
sources:
  schema: acme-billing-db:procedures/usp_GetCustomerInvoices.sql@7d1ddd0
links:
  - type: defines
    to: databases/acme-billing-db/database
    basis: extracted
    source: schema
verified:
  commit: "7d1ddd0"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Files: 4. Most common: `.sql` 4.
<!-- /extracted -->
