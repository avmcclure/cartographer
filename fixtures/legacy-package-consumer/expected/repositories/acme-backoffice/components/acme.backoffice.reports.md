---
kind: component
name: Acme.Backoffice.Reports
type: library
summary: 'Acme.Backoffice.Reports: a library in acme-backoffice; not yet described.'
repository: repositories/acme-backoffice/repository
stack:
  - vbnet
  - net472
sources:
  proj: acme-backoffice:Acme.Backoffice.Reports/Acme.Backoffice.Reports.vbproj@a83d937
provides:
  - name: Acme.Backoffice.Reports
    as: assembly
    basis: extracted
    source: proj#L7
  - name: Acme.Backoffice.Reports
    as: namespace
    basis: extracted
    source: proj#L6
links:
  - type: depends-on
    name: '{00020813-0000-0000-C000-000000000046}'
    version: "1.9"
    basis: extracted
    source: proj#L20
verified:
  commit: "a83d937"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Project: `Acme.Backoffice.Reports/Acme.Backoffice.Reports.vbproj` (legacy style).
- Output type: Library.
- Target frameworks: net472.
- NuGet feeds from nuget.config: `acme-internal` https://nuget.acme.example/v3/index.json, `nuget.org` https://api.nuget.org/v3/index.json.
<!-- /extracted -->

## Open questions

### q1

- State: open
- Raised by: cli
- Attempts: 0
- Missing: Which component provides '{00020813-0000-0000-C000-000000000046}'. No scanned repository provides it.
- Waiting on: name {00020813-0000-0000-C000-000000000046}
- Answer looks like: A component whose provides list contains {00020813-0000-0000-C000-000000000046}, or a person stating it comes from outside the organisation.
- Answer:
