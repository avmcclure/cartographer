---
kind: repository
name: sales-order-service
summary: The order API, its background worker and the contracts library other teams build against.
stack:
  - csharp
  - net8.0
locations:
  - id: github:brackley/sales-order-service
    primary: true
verified:
  commit: "4c1e9a2"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

The modern half of Ordering. The API takes orders from the web shop, the worker settles payments in the background, and the contracts library is the shared description of an order that Billing and Warehouse compile against.

## Technical detail

<!-- extracted -->
| Files | Count |
|---|---|
| .cs | 212 |
| .csproj | 3 |
<!-- /extracted -->
