---
kind: table
name: dbo.OrdersAudit
summary: A history row for every change to an order, written by a trigger.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  scripts: sales-storefront:db/009_orders_audit.sql@9be0713
drift:
  - what: The live table has a column ChangedBy nvarchar(128) that no script in the repository creates.
    sources:
      - scripts
      - live
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Keeps the trail of what happened to each order. Nothing writes to it directly; a trigger on Orders fills it.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| AuditId | bigint | no | PK |
| OrderId | int | no |  |
| ChangedAt | datetime2 | no |  |
| OldStatus | varchar(20) | yes |  |
| NewStatus | varchar(20) | yes |  |
<!-- /extracted -->

The columns above are the repository's. The test server's copy has one more column; see the drift note in the frontmatter.
