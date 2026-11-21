---
kind: trigger
name: dbo.trg_Orders_Audit
summary: Writes a history row whenever an order's status changes.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  scripts: sales-storefront:db/010_trg_orders_audit.sql@9be0713
links:
  - type: fires-on
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: scripts#L3
  - type: writes
    to: databases/sales/tables/dbo/orders-audit
    basis: extracted
    source: scripts#L9-L12
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Runs by itself after every update to Orders. No program calls it, which makes it easy to forget when the Orders table changes.

## Technical detail

<!-- extracted -->
`AFTER UPDATE` on `dbo.Orders`.
<!-- /extracted -->
