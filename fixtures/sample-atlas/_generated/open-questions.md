# Open questions

## needs-person (3)

| Entry | Question | Missing | Waiting on | Attempts |
|---|---|---|---|---|
| databases/sales/tables/dbo/orders | q1 | Whether anything outside the Ordering system still writes to dbo.Orders. | person | 3 |
| external/paystream | q1 | Which version of the Paystream API Brackley is contracted to use; the client class names no version. | person | 3 |
| repositories/warehouse-stock/components/stock-sync | q1 | Which system StockSync belongs to: Warehouse, or a reporting system that is not in the atlas yet. | person | 3 |

## answered (1)

| Entry | Question | Missing | Waiting on | Attempts |
|---|---|---|---|---|
| databases/sales/database | q1 | Whether SALES_PROD on sql-prod-02 is this database or a separate reporting copy. | person | 2 |

## open (4)

| Entry | Question | Missing | Waiting on | Attempts |
|---|---|---|---|---|
| databases/ledger/database | q1 | Which repository holds the scripts that create the Ledger database. | repository repositories/finance-invoicing/repository | 1 |
| repositories/finance-invoicing/components/invoice-job | q1 | Which component provides the package Brackley.Tax. | name Brackley.Tax | 0 |
| repositories/sales-storefront/components/backoffice | q1 | Whether BackOffice still sends mail through Brackley.Mailer now that the mailer is retired. | entry repositories/platform-common/components/legacy-mailer | 1 |
| repositories/warehouse-stock/components/stock-sync | q2 | Where StockSync writes its recount: the Stock connection points at a database that has no entry. | name Stock | 1 |
