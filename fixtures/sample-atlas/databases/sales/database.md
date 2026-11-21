---
kind: database
name: Sales
summary: Customers, their orders and the payments against them. The one database both halves of Ordering write to.
engine: sqlserver
aliases:
  - SALES_PROD
  - Sales_*
instances:
  - name: sql-test-01
    environment: test
    read: "2026-11-09"
    permission: definitions
sources:
  live: instance:sql-test-01@2026-11-09
  said: person:m.okafor@2026-10-02
  scripts: sales-storefront:db/001_create.sql@9be0713
systems:
  - id: systems/ordering
    basis: stated
    source: said
owner:
  name: Sales Systems team
  basis: stated
  source: said
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

The record of what customers have ordered and paid. It is built from numbered scripts in the storefront repository and has also been read from the test server, so each fact about it can be checked against both.

## Technical detail

<!-- extracted -->
Defined by 27 numbered scripts under `db/`. 5 tables, 1 view, 2 routines, 1 trigger.
<!-- /extracted -->

## Open questions

### q1

- State: answered
- Raised by: agent
- Attempts: 2
- Missing: Whether SALES_PROD on sql-prod-02 is this database or a separate reporting copy.
- Waiting on: person
- Answer looks like: A statement from someone who administers sql-prod-02.
- Answer: It is this database. SALES_PROD is only the name of the linked-server login. (M. Okafor)
