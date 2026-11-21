---
kind: component
name: WebShop
type: site
summary: The public web shop where trade customers browse the catalogue and place orders.
repository: repositories/sales-storefront/repository
stack:
  - classic-asp
  - vbscript
sources:
  account: sales-storefront:shop/account.asp@9be0713
  checkout: sales-storefront:shop/checkout.asp@9be0713
  conn: sales-storefront:shop/includes/conn.asp@9be0713
  orders: sales-storefront:shop/orders.asp@9be0713
  said: person:m.okafor@2026-10-02
systems:
  - id: systems/ordering
    basis: stated
    source: said
links:
  - type: uses
    to: databases/sales/database
    basis: extracted
    source: conn#L4
  - type: reads
    to: databases/sales/tables/dbo/customers
    basis: extracted
    source: account#L31-L36
  - type: reads
    to: databases/sales/tables/dbo/open-orders
    basis: extracted
    source: orders#L18
  - type: writes
    to: databases/sales/tables/dbo/orders
    basis: inferred
    source: checkout#L212
  - type: calls
    to: databases/sales/routines/dbo/usp-closeorder
    how: sql
    basis: extracted
    source: orders#L77
  - type: calls
    to: repositories/sales-order-service/components/order-api
    how: http
    basis: inferred
    source: checkout#L140-L158
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

The shop trade customers use. They sign in, search the catalogue, fill a basket and check out. New orders are sent to the order API; a few older pages still talk to the Sales database directly.

## Technical detail

<!-- extracted -->
64 `.asp` pages under `shop/`. One shared connection include, `shop/includes/conn.asp`: server `sql-prod-02`, database `Sales`.
<!-- /extracted -->

Checkout builds its `INSERT INTO Orders` statement as a string, so the write to Orders is inferred from the text, not read from a parsed statement. The call to the order API goes through `MSXML2.ServerXMLHTTP`; the address comes from an application variable set outside the repository.
