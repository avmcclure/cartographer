---
kind: component
name: OrderGateway
type: service
summary: A reverse proxy that passes the shop's order requests to the order API unchanged.
repository: repositories/sales-edge/repository
stack:
  - csharp
  - net8.0
  - yarp
sources:
  routes: sales-edge:gateway/appsettings.json@4c2e9b0
links:
  - type: forwards-to
    to: repositories/sales-order-service/components/order-api
    how: http
    uses:
      - GET /orders/{id}
      - POST /orders
    basis: extracted
    source: routes#L8-L31
verified:
  commit: "4c2e9b0"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

A YARP reverse proxy. Every request under `/orders` goes to the order API as it came in. It exists so the order API can be replaced behind it without changing the shop.

## Technical detail

<!-- extracted -->
Project `gateway/OrderGateway.csproj`, net8.0.
<!-- /extracted -->

Routes and clusters are in `gateway/appsettings.json` under `ReverseProxy`. There is one cluster, `orders`, whose destination address is set per environment outside the repository.
