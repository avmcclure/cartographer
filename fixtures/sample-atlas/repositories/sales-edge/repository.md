---
kind: repository
name: sales-edge
summary: The gateway in front of the order API, and the customer lookup service.
stack:
  - csharp
  - net8.0
locations:
  - id: github:brackley/sales-edge
    primary: true
verified:
  commit: "4c2e9b0"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

Two small services that sit between the shop and the order service. The gateway was put in front of the order API so the shop's address for it never changes while orders move to a new service; the customer lookup answers who a customer is and what terms they buy on.

## Technical detail

<!-- extracted -->
| Files | Count |
|---|---|
| .cs | 31 |
| .csproj | 2 |
| .json | 4 |
<!-- /extracted -->
