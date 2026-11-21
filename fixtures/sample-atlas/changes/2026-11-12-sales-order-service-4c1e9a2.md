# sales-order-service at 4c1e9a2

Scanned on 2026-11-12, changed files only (previous scan: 1b7e3f0).

## Added

- repositories/sales-order-service/components/order-worker

## Changed

- repositories/sales-order-service/components/order-api: now writes dbo.OrderLines; no longer writes dbo.Payments.
- channels/order-events: gained a consumer.

## Questions answered

- databases/sales/database q1: SALES_PROD is this database.
