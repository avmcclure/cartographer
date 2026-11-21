---
kind: component
name: Brackley.Stock.Service
type: service
summary: Reserves stock for each new order and answers the question "can we ship this today?".
repository: repositories/warehouse-stock/repository
stack:
  - csharp
  - net8.0
sources:
  handler: warehouse-stock:src/Brackley.Stock.Service/OrderPlacedHandler.cs@a83f6d1
  lines: warehouse-stock:src/Brackley.Stock.Service/OrderLineReader.cs@a83f6d1
  proj: warehouse-stock:src/Brackley.Stock.Service/Brackley.Stock.Service.csproj@a83f6d1
  said: person:d.lindqvist@2026-10-15
systems:
  - id: systems/warehouse
    basis: stated
    source: said
links:
  - type: depends-on
    to: repositories/sales-order-service/components/order-contracts
    name: Brackley.Orders.Contracts
    version: "3.1.0"
    uses:
      - OrderPlaced
      - OrderLine
    count: 12
    basis: extracted
    source: proj#L13
  - type: depends-on
    to: repositories/platform-common/components/brackley-common
    name: Brackley.Common
    version: "5.1.0"
    uses:
      - Clock
    count: 3
    basis: extracted
    source: proj#L16
  - type: consumes-from
    to: channels/order-events
    basis: extracted
    source: handler#L17
  - type: reads
    to: databases/sales/tables/dbo/order-lines
    basis: inferred
    source: lines#L28-L52
  - type: calls
    to: repositories/sales-order-service/components/order-api
    how: http
    basis: stated
    source: said
verified:
  commit: "a83f6d1"
  date: "2026-11-05"
  cartographer: "0.1.0"
---

## Overview

When an order is placed this service sets stock aside for it. Pickers' handheld scanners ask it what to pull.

## Technical detail

<!-- extracted -->
Target framework: net8.0. Worker service with a small HTTP surface, 4 endpoints.
<!-- /extracted -->

The order lines query is built with string interpolation over a table name held in configuration, so the read of `dbo.OrderLines` is inferred.

<!-- person -->
It also calls the order API to mark an order as part-shipped. That goes through the API gateway, which is why no address for it appears in this repository. (D. Lindqvist, 15 October 2026)
<!-- /person -->
