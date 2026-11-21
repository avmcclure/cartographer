---
kind: table
name: dbo.Payments
summary: Money received against orders, by card through Paystream or keyed in by a clerk.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  said: person:m.okafor@2026-10-02
  scripts: sales-storefront:db/006_payments.sql@9be0713
tags:
  - name: payment-data
    basis: stated
    source: said
links:
  - type: references
    to: databases/sales/tables/dbo/orders
    name: FK_Payments_Orders
    basis: extracted
    source: scripts#L19
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

One row each time money arrives for an order. Card payments carry Paystream's reference; card numbers are never stored here.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| PaymentId | int | no | PK |
| OrderId | int | no | FK |
| Amount | decimal(18,2) | no |  |
| Method | varchar(16) | no |  |
| PaystreamRef | varchar(64) | yes |  |
| ReceivedAt | datetime2 | no |  |
<!-- /extracted -->
