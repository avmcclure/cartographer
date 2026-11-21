---
kind: flow
name: Order to invoice
summary: How an order placed in the web shop becomes an invoice in the ledger the next morning.
sources:
  said: person:m.okafor@2026-10-02
verified:
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

1. A customer checks out in the web shop (`repositories/sales-storefront/components/webshop`), which sends the order to the order API.
2. The order API (`repositories/sales-order-service/components/order-api`) stores it in `dbo.Orders` and `dbo.OrderLines`, asks Paystream to authorise the card and puts a message on `channels/order-events`.
3. The stock service reserves stock; the payment worker records the payment when Paystream confirms it.
4. When the order ships, `dbo.usp_CloseOrder` closes it.
5. At 01:00 the invoice job (`repositories/finance-invoicing/components/invoice-job`) reads the closed orders and posts an invoice to the Ledger database.
