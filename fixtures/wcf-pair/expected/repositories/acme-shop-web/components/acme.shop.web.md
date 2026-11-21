---
kind: component
name: Acme.Shop.Web
type: site
summary: 'Acme.Shop.Web: a site in acme-shop-web; not yet described.'
repository: repositories/acme-shop-web/repository
stack:
  - csharp
  - net48
sources:
  config: acme-shop-web:Acme.Shop.Web/Web.config@b4cb410
  proj: acme-shop-web:Acme.Shop.Web/Acme.Shop.Web.csproj@b4cb410
provides:
  - name: Acme.Shop.Web
    as: assembly
    basis: extracted
    source: proj#L8
  - name: Acme.Shop.Web
    as: namespace
    basis: extracted
    source: proj#L7
links:
  - type: calls
    name: Acme.Orders.Contracts.IOrderService
    how: wcf
    basis: extracted
    source: config#L10
verified:
  commit: "b4cb410"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Project: `Acme.Shop.Web/Acme.Shop.Web.csproj` (legacy style).
- Output type: Library.
- Target frameworks: net48.
- WCF client endpoint (`Acme.Shop.Web/Web.config` line 10): contract `Acme.Orders.Contracts.IOrderService`, binding basicHttpBinding, address `http://orders.acme.example/OrderService.svc`.
<!-- /extracted -->
