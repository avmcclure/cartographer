---
kind: component
name: BackOffice
type: desktop-app
summary: The Windows program sales clerks use to look up customers, key in phone orders and record payments.
repository: repositories/sales-storefront/repository
stack:
  - vb6
sources:
  data: sales-storefront:backoffice/modData.bas@9be0713
  pay: sales-storefront:backoffice/frmPayment.frm@9be0713
  said: person:m.okafor@2026-10-02
  vbp: sales-storefront:backoffice/BackOffice.vbp@9be0713
systems:
  - id: systems/ordering
    basis: stated
    source: said
links:
  - type: uses
    to: databases/sales/database
    basis: extracted
    source: data#L12
  - type: reads
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: data#L88
  - type: reads
    to: databases/sales/tables/dbo/order-lines
    basis: extracted
    source: data#L96
  - type: reads
    to: databases/sales/tables/dbo/customers
    basis: extracted
    source: data#L41
  - type: writes
    to: databases/sales/tables/dbo/customers
    basis: extracted
    source: data#L57-L63
  - type: writes
    to: databases/sales/tables/dbo/payments
    basis: extracted
    source: pay#L120
  - type: depends-on
    to: repositories/platform-common/components/legacy-mailer
    name: Brackley.Mailer
    basis: extracted
    source: vbp#L9
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

A VB6 program installed on about twenty desks in the sales office. Clerks use it for phone orders and for anything the web shop cannot do, such as correcting a customer's address or recording a cheque.

## Technical detail

<!-- extracted -->
Project `backoffice/BackOffice.vbp`: 14 forms, 9 modules, 6 classes. References: `Brackley.Mailer` (COM), `Microsoft ActiveX Data Objects 2.8`.
<!-- /extracted -->

All data access is ADO with SQL text in `modData.bas`. It still references the mailer's type library, although the mailer project no longer exists.

## Open questions

### q1

- State: open
- Raised by: cli
- Attempts: 1
- Missing: Whether BackOffice still sends mail through Brackley.Mailer now that the mailer is retired.
- Waiting on: entry repositories/platform-common/components/legacy-mailer
- Answer looks like: The mailer's replacement, or a statement that the reference is dead code.
- Answer:
