# one-schema-three-forms

Three repositories of the invented company Acme that each describe the same small `Shop` database, in the three forms a SQL Server schema usually takes in source control. Nothing here is ever built or run.

| Repository | Form | Read by |
|---|---|---|
| `acme-shop-database` | SSDT projects: `Shop/Shop.sqlproj` and `Ledger/Ledger.sqlproj`, one file per object | the SQL schema extractor |
| `acme-shop-migrations` | Numbered upgrade scripts `db/001_….sql` onwards, each starting `USE Shop` | the SQL schema extractor |
| `acme-shop-api` | EF Core: a C# project with one migration and the model snapshot | other extractors only |

Reading EF Core migrations and snapshots is staged after v1 (spec section 1), so `acme-shop-api` contributes no database entries: it is here so that the reader has a fixture waiting, and so that the .NET extractors see it.

## The schema

`Shop` has five tables (`Customers`, `Products`, `Orders`, `OrderLines`, `OrderAudit`) with three foreign keys, the trigger `trg_Orders_Audit` on `Orders` that writes `OrderAudit`, the procedures `PlaceOrder` and `CompleteOrder`, the function `OrderTotal` and the view `OrderSummary`.

In the scripts form, `Orders.Status` arrives by `ALTER TABLE … ADD` (004) and the key from `OrderLines` to `Products` by `ALTER TABLE … ADD CONSTRAINT` (005). `008_seed_products.sql` holds only data and defines nothing.

## The deliberate difference

`dbo.Customers.Email` is `nvarchar(256)` in the SSDT project and `nvarchar(200)` in the upgrade scripts. Everything else agrees. A later step will detect this as drift; until then the entry shows the form scanned last (`acme-shop-migrations`).

## Other databases

- `Ledger` is a second database, defined in `acme-shop-database`. `Shop.dbo.CompleteOrder` calls `Ledger.dbo.PostEntry` by three-part name. The extractor leaves that link unresolved with a database hint and the resolver fills it in.
- `Warehouse` is defined nowhere. `CompleteOrder` updates `Warehouse.dbo.StockLevels`, and that link stays unresolved.
