# Used by: databases/sales/tables/dbo/order-lines

Direct users only. For indirect reach run `cartographer impact databases/sales/tables/dbo/order-lines`.

## Read by

- databases/sales/routines/dbo/fn-ordertotal (extracted)
- databases/sales/routines/dbo/usp-closeorder (extracted)
- repositories/finance-invoicing/components/invoice-job (extracted)
- repositories/sales-storefront/components/backoffice (extracted)
- repositories/warehouse-stock/components/stock-service (inferred)
- repositories/warehouse-stock/components/stock-sync (extracted)

## Written by

- repositories/sales-order-service/components/order-api (extracted)
