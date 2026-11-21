---
kind: table
name: dbo.Customers
summary: The trade customers Brackley sells to, with contact names, addresses and credit terms.
database: databases/sales/database
sources:
  live: instance:sql-test-01@2026-11-09
  said: person:m.okafor@2026-10-02
  scripts: sales-storefront:db/002_customers.sql@9be0713
tags:
  - name: personal-data
    basis: stated
    source: said
  - name: customer-data
    basis: inferred
    source: scripts#L8-L15
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

Who Brackley sells to. It holds names, email addresses and phone numbers of the people who place orders, so it counts as personal data.

## Technical detail

<!-- extracted -->
| Column | Type | Nullable | Key |
|---|---|---|---|
| CustomerId | int | no | PK |
| Company | nvarchar(120) | no |  |
| ContactName | nvarchar(120) | yes |  |
| Email | nvarchar(254) | yes |  |
| Phone | varchar(32) | yes |  |
| CreditLimit | decimal(18,2) | no |  |
<!-- /extracted -->
