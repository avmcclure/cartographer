---
kind: routine
name: dbo.usp_CloseOrder
type: procedure
summary: Marks an order as closed once it has shipped, after checking its total against its lines.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  scripts: sales-storefront:db/014_usp_closeorder.sql@9be0713
links:
  - type: writes
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: scripts#L18-L22
  - type: reads
    to: databases/sales/tables/dbo/order-lines
    basis: extracted
    source: scripts#L11
  - type: calls
    to: databases/sales/routines/dbo/fn-ordertotal
    how: sql
    basis: extracted
    source: scripts#L11
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

The one place an order is closed. Both the web shop and the order API call it.

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @OrderId | int | in |
| @ClosedAt | datetime2 | in |
<!-- /extracted -->
