---
kind: component
name: Acme.Backoffice.Web
type: site
summary: 'Acme.Backoffice.Web: a site in acme-backoffice; not yet described.'
repository: repositories/acme-backoffice/repository
stack:
  - csharp
  - net472
sources:
  config: acme-backoffice:Acme.Backoffice.Web/Web.config@a83d937
  packages: acme-backoffice:Acme.Backoffice.Web/packages.config@a83d937
  proj: acme-backoffice:Acme.Backoffice.Web/Acme.Backoffice.Web.csproj@a83d937
provides:
  - name: Acme.Backoffice.Web
    as: assembly
    basis: extracted
    source: proj#L10
  - name: Acme.Backoffice.Web
    as: namespace
    basis: extracted
    source: proj#L9
packages:
  - Newtonsoft.Json 12.0.3
links:
  - type: depends-on
    to: repositories/acme-backoffice/components/acme.backoffice.reports
    basis: extracted
    source: proj#L41
  - type: depends-on
    to: repositories/acme-pricing/components/acme.pricing
    name: Acme.Pricing
    version: "2.1.0"
    basis: extracted
    source: packages#L3
  - type: depends-on
    name: Acme.Legacy.Tax
    version: "1.3.0.0"
    basis: extracted
    source: proj#L14
  - type: uses
    name: Backoffice
    basis: extracted
    source: config#L4
  - type: uses
    name: Reporting
    basis: extracted
    source: config#L7
verified:
  commit: "a83d937"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- Project: `Acme.Backoffice.Web/Acme.Backoffice.Web.csproj` (legacy style).
- Output type: Library.
- Target frameworks: net472.
- NuGet feeds from nuget.config: `acme-internal` https://nuget.acme.example/v3/index.json, `nuget.org` https://api.nuget.org/v3/index.json.
- Assemblies referenced by file path, origin unknown: `../lib/Acme.Legacy.Tax.dll`.
- Connection string `Backoffice` (`Acme.Backoffice.Web/Web.config` line 4): server `sql-bo-01.acme.example`, database `Backoffice`, a credential is present (not recorded).
- Connection string `Reporting` (`Acme.Backoffice.Web/Web.config` line 7): server `sql-rep-01.acme.example`, database `Reporting`, no credential.
<!-- /extracted -->

## Open questions

### q1

- State: open
- Raised by: cli
- Attempts: 0
- Missing: Which component provides 'Acme.Legacy.Tax'. It matches an internal name prefix but no scanned repository provides it.
- Waiting on: name Acme.Legacy.Tax
- Answer looks like: A component whose provides list contains Acme.Legacy.Tax, or a person stating it is public.
- Answer:
