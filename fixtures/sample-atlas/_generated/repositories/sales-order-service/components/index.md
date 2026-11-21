# repositories/sales-order-service/components

## Entries

| ID | Kind | Name | Summary |
|---|---|---|---|
| repositories/sales-order-service/components/order-api | component (service) | Brackley.Orders.Api | The HTTP service that accepts, prices and stores orders, takes payment and announces each new order. |
| repositories/sales-order-service/components/order-contracts | component (library) | Brackley.Orders.Contracts | The shared description of an order and its events, published as a package for other teams. |
| repositories/sales-order-service/components/order-worker | component (job) | Brackley.Orders.Worker | The background worker that settles payments for placed orders and records the result. |
