# databases/sales/tables/dbo

## Entries

| ID | Kind | Name | Summary |
|---|---|---|---|
| databases/sales/tables/dbo/customers | table | dbo.Customers | The trade customers Brackley sells to, with contact names, addresses and credit terms. |
| databases/sales/tables/dbo/open-orders | view | dbo.OpenOrders | Orders that have not shipped yet, joined to the customer's company name. |
| databases/sales/tables/dbo/order-lines | table | dbo.OrderLines | The products on each order, one row per product and quantity. |
| databases/sales/tables/dbo/orders | table | dbo.Orders | Every customer order, one row per order. Created at checkout, closed when the order ships. |
| databases/sales/tables/dbo/orders-audit | table | dbo.OrdersAudit | A history row for every change to an order, written by a trigger. |
| databases/sales/tables/dbo/payments | table | dbo.Payments | Money received against orders, by card through Paystream or keyed in by a clerk. |
