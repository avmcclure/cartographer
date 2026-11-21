---
kind: component
name: Acme.Orders.Service
type: service
summary: 'Acme.Orders.Service: a service in acme-orders-service; not yet described.'
repository: repositories/acme-orders-service/repository
stack:
  - csharp
  - net48
sources:
  config: acme-orders-service:Acme.Orders.Service/Web.config@0b58c9f
  proj: acme-orders-service:Acme.Orders.Service/Acme.Orders.Service.csproj@0b58c9f
provides:
  - name: Acme.Orders
    as: namespace
    basis: extracted
    source: proj#L7
  - name: Acme.Orders.Contracts.IOrderService
    as: contract
    basis: extracted
    source: config#L9
  - name: Acme.Orders.Service
    as: assembly
    basis: extracted
    source: proj#L8
links:
  - type: uses
    name: Orders
    basis: extracted
    source: config#L4
verified:
  commit: "0b58c9f"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Project: `Acme.Orders.Service/Acme.Orders.Service.csproj` (legacy style).
- Output type: Library.
- Target frameworks: net48.
- Connection string `Orders` (`Acme.Orders.Service/Web.config` line 4): server `sql-ord-01.acme.example`, database `Orders`, no credential.
- WCF service `Acme.Orders.OrderService` (`Acme.Orders.Service/Web.config` line 9): contract `Acme.Orders.Contracts.IOrderService`, binding basicHttpBinding, address not given.
<!-- /extracted -->
