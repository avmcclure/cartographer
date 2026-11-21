---
kind: view
name: dbo.OpenOrders
summary: Orders that have not shipped yet, joined to the customer's company name.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  scripts: sales-storefront:db/011_open_orders.sql@9be0713
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

A view the web shop uses for its "my open orders" page.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| OrderId | int | no |  |
| Company | nvarchar(120) | no |  |
| PlacedAt | datetime2 | no |  |
| Total | decimal(18,2) | no |  |
<!-- /extracted -->

Selects from `dbo.Orders` and `dbo.Customers` where `Status` is not `Closed`.
