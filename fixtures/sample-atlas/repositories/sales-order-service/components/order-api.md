---
kind: component
name: Brackley.Orders.Api
type: service
summary: The HTTP service that accepts, prices and stores orders, takes payment and announces each new order.
repository: repositories/sales-order-service/repository
stack:
  - csharp
  - net8.0
sources:
  events: sales-order-service:src/Brackley.Orders.Api/Events/OrderPublisher.cs@4c1e9a2
  pay: sales-order-service:src/Brackley.Orders.Api/Payments/PaystreamClient.cs@4c1e9a2
  proj: sales-order-service:src/Brackley.Orders.Api/Brackley.Orders.Api.csproj@4c1e9a2
  repo: sales-order-service:src/Brackley.Orders.Api/Data/OrderRepository.cs@4c1e9a2
  said: person:m.okafor@2026-10-02
  settings: sales-order-service:src/Brackley.Orders.Api/appsettings.json@4c1e9a2
systems:
  - id: systems/ordering
    basis: stated
    source: said
packages:
  - Dapper 2.1.35
  - Azure.Messaging.ServiceBus 7.18.1
links:
  - type: depends-on
    to: repositories/sales-order-service/components/order-contracts
    name: Brackley.Orders.Contracts
    basis: extracted
    source: proj#L14
  - type: depends-on
    to: repositories/platform-common/components/brackley-common
    name: Brackley.Common
    version: "5.1.0"
    uses:
      - Clock
      - Money
      - RetryPolicy
    count: 23
    basis: extracted
    source: proj#L18
  - type: uses
    to: databases/sales/database
    basis: extracted
    source: settings#L7
  - type: reads
    to: databases/sales/tables/dbo/customers
    basis: extracted
    source: repo#L40-L58
  - type: reads
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: repo#L61-L79
  - type: writes
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: repo#L84-L103
  - type: writes
    to: databases/sales/tables/dbo/order-lines
    basis: extracted
    source: repo#L105-L121
  - type: calls
    to: databases/sales/routines/dbo/usp-closeorder
    how: sql
    basis: extracted
    source: repo#L130
  - type: publishes-to
    to: channels/order-events
    basis: extracted
    source: events#L22
  - type: sends-to
    to: external/paystream
    basis: inferred
    source: pay#L35-L60
verified:
  commit: "4c1e9a2"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

The front door for new orders. It checks the customer, prices the basket, stores the order, asks Paystream to authorise the card and then announces the order so that Billing and Warehouse can act on it.

## Technical detail

<!-- extracted -->
Target framework: net8.0. ASP.NET Core minimal API, 11 endpoints. Connection string `Sales`: server `sql-prod-02`, database `Sales`.
<!-- /extracted -->

Data access is Dapper with SQL text in `OrderRepository.cs`, parsed statement by statement. The Paystream address is read from configuration that is not in the repository, so that link is inferred from the client class.
