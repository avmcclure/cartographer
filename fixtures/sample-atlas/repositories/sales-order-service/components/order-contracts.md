---
kind: component
name: Brackley.Orders.Contracts
type: library
summary: The shared description of an order and its events, published as a package for other teams.
repository: repositories/sales-order-service/repository
stack:
  - csharp
  - netstandard2.0
sources:
  proj: sales-order-service:src/Brackley.Orders.Contracts/Brackley.Orders.Contracts.csproj@4c1e9a2
  said: person:m.okafor@2026-10-02
provides:
  - name: Brackley.Orders.Contracts
    as: nuget
    basis: extracted
    source: proj#L5
  - name: Brackley.Orders.Contracts
    as: assembly
    basis: extracted
    source: proj
systems:
  - id: systems/ordering
    basis: stated
    source: said
verified:
  commit: "4c1e9a2"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

The types that describe an order to the rest of Brackley: the order itself, its lines, and the "order placed" message. Billing and Warehouse compile against this, so a change here reaches further than the Ordering system.

## Technical detail

<!-- extracted -->
Target framework: netstandard2.0. Package version 3.2.0.
<!-- /extracted -->
