---
kind: database
name: Ledger
summary: Invoices and the ledger postings behind them. Known only from the test server; no repository defines it.
engine: sqlserver
instances:
  - name: sql-test-01
    environment: test
    read: "2026-11-09"
    permission: limited
sources:
  live: instance:sql-test-01@2026-11-09
  said: person:m.okafor@2026-10-02
systems:
  - id: systems/billing
    basis: stated
    source: said
links:
  - type: links-to
    to: databases/sales/database
    basis: extracted
    source: live
verified:
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Finance's record of what has been invoiced. Everything known about it was read from the test server, with a login that may see table and column names but not the text of routines.

## Technical detail

<!-- extracted -->
2 tables, 1 routine. Linked server `SALES_PROD` points at the Sales database.
<!-- /extracted -->

## Open questions

### q1

- State: open
- Raised by: agent
- Attempts: 1
- Missing: Which repository holds the scripts that create the Ledger database.
- Waiting on: repository repositories/finance-invoicing/repository
- Answer looks like: A repository with SQL scripts or a database project for Ledger.
- Answer:
