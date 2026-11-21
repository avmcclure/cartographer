---
kind: component
name: Acme.Pricing
type: library
summary: 'Acme.Pricing: a library in acme-pricing; not yet described.'
repository: repositories/acme-pricing/repository
stack:
  - csharp
  - netstandard2.0
  - net8.0
sources:
  proj: acme-pricing:src/Acme.Pricing/Acme.Pricing.csproj@7747bee
provides:
  - name: Acme.Pricing
    as: assembly
    basis: extracted
    source: proj#L1
  - name: Acme.Pricing
    as: namespace
    basis: extracted
    source: proj#L4
  - name: Acme.Pricing
    as: nuget
    basis: extracted
    source: proj#L6
packages:
  - Newtonsoft.Json 13.0.3
  - System.Memory 4.5.5
verified:
  commit: "7747bee"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Project: `src/Acme.Pricing/Acme.Pricing.csproj` (SDK style, Microsoft.NET.Sdk).
- Output type: Library (default).
- Target frameworks: netstandard2.0, net8.0.
<!-- /extracted -->
