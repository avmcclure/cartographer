---
kind: table
name: dbo.Invoices
summary: One row per invoice raised, with the order it bills and the customer's billing name.
database: databases/ledger/database
sources:
  job: finance-invoicing:InvoiceRun/InvoiceRun.vb@71d20ce
  live: instance:sql-test-01@2026-11-09
tags:
  - name: invoice-data
    basis: inferred
    source: job#L120-L131
  - name: personal-data
    basis: inferred
    source: live
verified:
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Each invoice Brackley has sent. It repeats the customer's billing name and address as they were on the day, so it holds personal data too.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| InvoiceId | int | no | PK |
| OrderId | int | no |  |
| BillingName | nvarchar(120) | no |  |
| BillingAddress | nvarchar(400) | no |  |
| Net | decimal(18,2) | no |  |
| Tax | decimal(18,2) | no |  |
| RaisedAt | datetime2 | no |  |
<!-- /extracted -->
