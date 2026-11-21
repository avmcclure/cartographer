---
kind: routine
name: dbo.usp_PostInvoice
type: procedure
summary: Stores one invoice and its ledger lines in a single transaction.
database: databases/ledger/database
sources:
  job: finance-invoicing:InvoiceRun/InvoiceRun.vb@71d20ce
  live: instance:sql-test-01@2026-11-09
links:
  - type: writes
    to: databases/ledger/tables/dbo/invoices
    basis: inferred
    source: job#L120-L131
  - type: writes
    to: databases/ledger/tables/dbo/ledger-entries
    basis: inferred
    source: job#L120-L131
verified:
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

The only way the invoice job writes to the ledger.

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @OrderId | int | in |
| @Net | decimal(18,2) | in |
| @Tax | decimal(18,2) | in |
| @InvoiceId | int | out |
<!-- /extracted -->

The login used for the live read may not see routine text, so what this procedure writes is inferred from its name, its parameters and how the invoice job uses the result.
