---
kind: table
name: dbo.LedgerEntries
summary: The debit and credit lines posted for each invoice.
database: databases/ledger/database
sources:
  live: instance:sql-test-01@2026-11-09
links:
  - type: references
    to: databases/ledger/tables/dbo/invoices
    name: FK_LedgerEntries_Invoices
    basis: extracted
    source: live
verified:
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

The accounting lines behind every invoice: at least one debit and one credit each.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| EntryId | bigint | no | PK |
| InvoiceId | int | no | FK |
| Account | varchar(12) | no |  |
| Debit | decimal(18,2) | no |  |
| Credit | decimal(18,2) | no |  |
<!-- /extracted -->
