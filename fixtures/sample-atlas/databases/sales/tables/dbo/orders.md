---
kind: table
name: dbo.Orders
summary: Every customer order, one row per order. Created at checkout, closed when the order ships.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  said: person:m.okafor@2026-10-02
  scripts: sales-storefront:db/003_orders.sql@9be0713
tags:
  - name: order-data
    basis: stated
    source: said
links:
  - type: references
    to: databases/sales/tables/dbo/customers
    name: FK_Orders_Customers
    basis: extracted
    source: scripts#L41
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

One row for each order a customer has placed. The web shop and the order API create rows; closing an order is done by a stored procedure. Billing and Warehouse both read it.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| OrderId | int | no | PK |
| CustomerId | int | no | FK |
| PlacedAt | datetime2 | no |  |
| Status | varchar(20) | no |  |
| Total | decimal(18,2) | no |  |
| ClosedAt | datetime2 | yes |  |
<!-- /extracted -->

## Open questions

### q1

- State: needs-person
- Raised by: agent
- Attempts: 3
- Missing: Whether anything outside the Ordering system still writes to dbo.Orders.
- Waiting on: person
- Answer looks like: A list of the programs with write permission on the table, from whoever administers sql-prod-02.
- Answer:
