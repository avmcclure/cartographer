# Used by: databases/sales/tables/dbo/orders

Direct users only. For indirect reach run `cartographer impact databases/sales/tables/dbo/orders`.

## Triggers

- databases/sales/triggers/dbo/trg-orders-audit (extracted)

## Read by

- repositories/finance-invoicing/components/invoice-job (extracted)
- repositories/sales-order-service/components/order-api (extracted)
- repositories/sales-storefront/components/backoffice (extracted)
- repositories/warehouse-stock/components/stock-sync (extracted)

## Referenced by

- databases/sales/tables/dbo/order-lines (extracted)
- databases/sales/tables/dbo/payments (extracted)

## Written by

- databases/sales/routines/dbo/usp-closeorder (extracted)
- repositories/sales-order-service/components/order-api (extracted)
- repositories/sales-storefront/components/webshop (inferred)
