---
kind: component
name: acme-intranet
type: site
summary: 'acme-intranet: a site in acme-intranet; not yet described.'
repository: repositories/acme-intranet/repository
stack:
  - classic-asp
sources:
  db-inc: acme-intranet:includes/db.inc@40bfc99
  default-asp: acme-intranet:default.asp@40bfc99
  invoices-asp: acme-intranet:invoices.asp@40bfc99
links:
  - type: calls
    to: databases/acme-billing-db/routines/dbo/usp_getcustomerinvoices
    name: dbo.usp_GetCustomerInvoices
    how: sql
    basis: extracted
    source: invoices-asp#L16
  - type: calls
    to: repositories/acme-billing-com/components/acmebilling
    name: AcmeBilling.Invoice
    how: com
    basis: extracted
    source: invoices-asp#L9
  - type: reads
    to: databases/acme-billing-db/tables/dbo/invoices
    name: dbo.Invoices
    basis: extracted
    source: default-asp#L7
  - type: uses
    name: AcmeBilling
    basis: extracted
    source: db-inc#L10
verified:
  commit: "40bfc99"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Classic ASP: 2 pages, 3 other script files (`.asa`, `.inc`); `global.asa` present.
- Includes: 2 files are included by others. Most included, with the number of files including each: `includes/db.inc` (2), `includes/header.inc` (2).
- Platform objects created: `ADODB.Command`, `ADODB.Connection`.
- 1 object created from a name built at run time, not linked: `invoices.asp` line 23.
- Connection string in `includes/db.inc` line 10: server `ACMESQL01`, database `AcmeBilling`; a credential is in the code (not recorded here).
- SQL built at run time, not linked: 1 statement in `invoices.asp` (1).
<!-- /extracted -->
