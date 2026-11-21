---
kind: component
name: Acme.Shop.Data
type: library
summary: 'Acme.Shop.Data: a library in acme-shop-api; not yet described.'
repository: repositories/acme-shop-api/repository
stack:
  - csharp
  - net8.0
sources:
  proj: acme-shop-api:src/Acme.Shop.Data/Acme.Shop.Data.csproj@23d6ced
provides:
  - name: Acme.Shop.Data
    as: assembly
    basis: extracted
    source: proj#L1
  - name: Acme.Shop.Data
    as: namespace
    basis: extracted
    source: proj#L1
packages:
  - Microsoft.EntityFrameworkCore.Design 8.0.10
  - Microsoft.EntityFrameworkCore.SqlServer 8.0.10
verified:
  commit: "23d6ced"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Project: `src/Acme.Shop.Data/Acme.Shop.Data.csproj` (SDK style, Microsoft.NET.Sdk).
- Output type: Library (default).
- Target frameworks: net8.0.
<!-- /extracted -->
