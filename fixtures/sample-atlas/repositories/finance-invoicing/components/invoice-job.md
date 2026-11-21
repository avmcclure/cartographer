---
kind: component
name: InvoiceRun
type: job
summary: The nightly job that raises an invoice for every shipped order and posts it to the ledger.
repository: repositories/finance-invoicing/repository
stack:
  - vbnet
  - net48
sources:
  config: finance-invoicing:InvoiceRun/App.config@71d20ce
  proj: finance-invoicing:InvoiceRun/InvoiceRun.vbproj@71d20ce
  queue: finance-invoicing:InvoiceRun/OrderFeed.vb@71d20ce
  run: finance-invoicing:InvoiceRun/InvoiceRun.vb@71d20ce
  said: person:m.okafor@2026-10-02
systems:
  - id: systems/billing
    basis: stated
    source: said
packages:
  - Newtonsoft.Json 13.0.3
links:
  - type: depends-on
    to: repositories/platform-common/components/brackley-common
    name: Brackley.Common
    version: "4.8.2"
    uses:
      - Money
    count: 9
    basis: extracted
    source: proj#L41
  - type: depends-on
    name: Brackley.Tax
    version: "1.3.0"
    basis: extracted
    source: proj#L44
  - type: uses
    to: databases/sales/database
    basis: extracted
    source: config#L6
  - type: uses
    to: databases/ledger/database
    basis: extracted
    source: config#L7
  - type: reads
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: run#L88
  - type: reads
    to: databases/sales/tables/dbo/order-lines
    basis: extracted
    source: run#L93-L97
  - type: calls
    to: databases/ledger/routines/dbo/usp-postinvoice
    how: sql
    basis: extracted
    source: run#L131
  - type: consumes-from
    to: channels/order-events
    basis: inferred
    source: queue#L15-L40
verified:
  commit: "71d20ce"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Every night at 01:00 this job finds the orders that shipped that day, works out tax, raises an invoice for each and posts the amounts to the ledger. If it fails, no invoices go out the next morning.

## Technical detail

<!-- extracted -->
Target framework: net48. Console application run by Windows Task Scheduler. Connection strings `Sales` (server `sql-prod-02`, database `Sales`) and `Ledger` (server `sql-prod-02`, database `Ledger`).
<!-- /extracted -->

`OrderFeed.vb` holds a queue client and a queue name read from configuration; nothing in the nightly path calls it, so whether the job listens for order events at all is inferred, not certain.

## Open questions

### q1

- State: open
- Raised by: cli
- Attempts: 0
- Missing: Which component provides the package Brackley.Tax.
- Waiting on: name Brackley.Tax
- Answer looks like: A component whose provides list contains Brackley.Tax, or a person stating where its source is kept.
- Answer:
