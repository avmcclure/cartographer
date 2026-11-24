---
kind: component
name: CustomerLookup
type: service
summary: Answers who a trade customer is and what credit terms they buy on.
repository: repositories/sales-edge/repository
stack:
  - csharp
  - net8.0
sources:
  query: sales-edge:customers/CustomerQueries.cs@4c2e9b0
links:
  - type: reads
    to: databases/sales/tables/dbo/customers
    how: sql
    basis: extracted
    source: query#L22-L40
verified:
  commit: "4c2e9b0"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

A read-only HTTP service over the Customers table. The shop and the order API ask it for a customer's name, delivery address and credit terms; nothing writes through it.

## Technical detail

<!-- extracted -->
Project `customers/CustomerLookup.csproj`, net8.0.
<!-- /extracted -->
