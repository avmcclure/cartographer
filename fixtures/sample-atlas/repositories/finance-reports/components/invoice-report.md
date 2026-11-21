---
kind: component
name: Invoices by month
type: report
summary: The month-end report finance staff run to list invoices and their ledger postings.
repository: repositories/finance-reports/repository
stack:
  - ssrs
sources:
  rdl: finance-reports:Reports/InvoicesByMonth.rdl@e05a1b8
  said: person:m.okafor@2026-10-02
systems:
  - id: systems/billing
    basis: stated
    source: said
links:
  - type: uses
    to: databases/ledger/database
    basis: extracted
    source: rdl#L12
  - type: reads
    to: databases/ledger/tables/dbo/invoices
    basis: extracted
    source: rdl#L31-L44
  - type: reads
    to: databases/ledger/tables/dbo/ledger-entries
    basis: extracted
    source: rdl#L31-L44
verified:
  commit: "e05a1b8"
  date: "2026-10-28"
  cartographer: "0.1.0"
---

## Overview

A Reporting Services report. Finance staff pick a month and get every invoice raised in it, with the ledger lines behind each.

## Technical detail

<!-- extracted -->
One dataset, one embedded query against data source `Ledger`.
<!-- /extracted -->
