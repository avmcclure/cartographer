---
kind: repository
name: storefront
summary: The public web shop and the sales back office program, with the Sales database scripts they share.
stack:
  - classic-asp
  - vbscript
  - vb6
  - sql
locations:
  - id: azdo:brackley/Sales/storefront
    primary: true
  - id: github:brackley/sales-storefront
    primary: false
sources:
  db: sales-storefront:db/001_create.sql@9be0713
links:
  - type: defines
    to: databases/sales/database
    basis: extracted
    source: db
verified:
  commit: "9be0713"
  date: "2026-11-09"
  cartographer: "0.1.0"
---

## Overview

The oldest code Brackley still runs. One repository holds the classic ASP web shop, the VB6 back office program that sales clerks use, and the numbered SQL scripts that built the Sales database.

It is being moved from Azure DevOps to GitHub; both copies exist and Azure DevOps is still the one people push to.

## Technical detail

<!-- extracted -->
| Files | Count |
|---|---|
| .asp | 64 |
| .bas, .cls, .frm | 41 |
| .sql | 27 |
<!-- /extracted -->
