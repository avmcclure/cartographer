---
kind: routine
name: dbo.fn_OrderTotal
type: function
summary: Adds up an order's lines and returns the total.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  scripts: sales-storefront:db/013_fn_ordertotal.sql@9be0713
links:
  - type: reads
    to: databases/sales/tables/dbo/order-lines
    basis: extracted
    source: scripts#L7-L9
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Quantity times unit price, summed over the order.

## Technical detail

<!-- extracted -->
| Parameter | Type | Direction |
|---|---|---|
| @OrderId | int | in |

Returns `decimal(18,2)`.
<!-- /extracted -->
