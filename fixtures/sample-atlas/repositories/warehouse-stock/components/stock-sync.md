---
kind: component
name: StockSync
type: etl
summary: The overnight package that recounts reserved stock from the day's orders.
repository: repositories/warehouse-stock/repository
stack:
  - ssis
sources:
  pkg: warehouse-stock:etl/StockSync/Recount.dtsx@a83f6d1
links:
  - type: reads
    to: databases/sales/tables/dbo/orders
    basis: extracted
    source: pkg#L212
  - type: reads
    to: databases/sales/tables/dbo/order-lines
    basis: extracted
    source: pkg#L240
  - type: uses
    to: databases/sales/database
    basis: extracted
    source: pkg#L31
verified:
  commit: "a83f6d1"
  date: "2026-11-05"
  cartographer: "0.1.0"
---

## Overview

An Integration Services package that runs at 03:00. It reads the day's orders and recounts what stock should be on reserve, as a check on the stock service.

## Technical detail

<!-- extracted -->
Project `etl/StockSync/StockSync.dtproj`: 3 packages. Connection manager `Sales`: server `sql-prod-02`, database `Sales`. A second connection manager, `Stock`, points at a database that is not in this atlas.
<!-- /extracted -->

## Open questions

### q1

- State: needs-person
- Raised by: agent
- Attempts: 3
- Missing: Which system StockSync belongs to: Warehouse, or a reporting system that is not in the atlas yet.
- Waiting on: person
- Answer looks like: The name of a system, from someone on the Warehouse Systems team.
- Answer:

### q2

- State: open
- Raised by: agent
- Attempts: 1
- Missing: Where StockSync writes its recount: the Stock connection points at a database that has no entry.
- Waiting on: name Stock
- Answer looks like: A database entry named Stock, or one that lists Stock among its aliases.
- Answer:
