---
kind: system
name: Billing
summary: Turns shipped orders into invoices and ledger postings, and prints the month-end invoice report.
sources:
  said: person:m.okafor@2026-10-02
owner:
  name: Finance Systems team
  basis: stated
  source: said
---

## Overview

Billing reads what Ordering has recorded, raises one invoice per shipped order and posts it to the ledger. Finance staff read the result through one report.

It is small: one nightly job, one report and one database.
