---
kind: component
name: Brackley.Orders.Worker
type: job
summary: The background worker that settles payments for placed orders and records the result.
repository: repositories/sales-order-service/repository
stack:
  - csharp
  - net8.0
sources:
  hook: person:j.petrov@2026-11-10
  proj: sales-order-service:src/Brackley.Orders.Worker/Brackley.Orders.Worker.csproj@4c1e9a2
  said: person:m.okafor@2026-10-02
  settle: sales-order-service:src/Brackley.Orders.Worker/SettlementHandler.cs@4c1e9a2
systems:
  - id: systems/ordering
    basis: stated
    source: said
links:
  - type: depends-on
    to: repositories/sales-order-service/components/order-contracts
    name: Brackley.Orders.Contracts
    basis: extracted
    source: proj#L12
  - type: depends-on
    to: repositories/platform-common/components/brackley-common
    name: Brackley.Common
    version: "5.1.0"
    uses:
      - RetryPolicy
    count: 4
    basis: extracted
    source: proj#L15
  - type: consumes-from
    to: channels/order-events
    basis: extracted
    source: settle#L19
  - type: writes
    to: databases/sales/tables/dbo/payments
    basis: extracted
    source: settle#L48-L66
  - type: receives-from
    to: external/paystream
    basis: stated
    source: hook
verified:
  commit: "4c1e9a2"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

Runs beside the API. When an order is placed it waits for Paystream to confirm that the money has moved, then records the payment against the order.

## Technical detail

<!-- extracted -->
Target framework: net8.0. Worker service, one hosted handler.
<!-- /extracted -->

<!-- person -->
Paystream calls us back on a webhook that is registered in their portal, not in code. You will not find the address in the repository. (J. Petrov, 10 November 2026)
<!-- /person -->

## Open questions

### Dismissed

- Whether the worker also reads dbo.Orders: no, it works from the event alone.
