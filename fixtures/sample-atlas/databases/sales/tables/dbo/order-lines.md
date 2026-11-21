---
kind: table
name: dbo.OrderLines
summary: The products on each order, one row per product and quantity.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  said: person:m.okafor@2026-10-02
  scripts: sales-storefront:db/004_order_lines.sql@9be0713
tags:
  - name: order-data
    basis: stated
    source: said
links:
  - type: references
    to: databases/sales/tables/dbo/orders
    name: FK_OrderLines_Orders
    basis: extracted
    source: scripts#L22
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

What was ordered: which product, how many, at what price. Warehouse reads these to know what to pick, Billing to know what to charge.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| OrderLineId | int | no | PK |
| OrderId | int | no | FK |
| Sku | varchar(32) | no |  |
| Quantity | int | no |  |
| UnitPrice | decimal(18,2) | no |  |
<!-- /extracted -->
