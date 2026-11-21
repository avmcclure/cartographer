---
kind: repository
summary: Source of Serilog.Extensions.Hosting, the package that plugs Serilog into Microsoft.Extensions.Hosting applications.
links: []
verified: {commit: d69d6f1, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This repository ships one small library, `Serilog.Extensions.Hosting`. Applications built on the standard .NET host add it so that the framework's own log messages go through Serilog alongside the application's. It also contains two sample applications that show how to wire it up.

## Technical detail

| Location | Identifier | Primary | Default branch |
|---|---|---|---|
| GitHub | `serilog/serilog-extensions-hosting` | yes | `dev` |

- Components: [`components/serilog-extensions-hosting`](components/serilog-extensions-hosting.md), built from `src/Serilog.Extensions.Hosting/Serilog.Extensions.Hosting.csproj`.
- `samples/SimpleServiceSample`, `samples/WebApplicationSample` and `test/Serilog.Extensions.Hosting.Tests` are not shipped and have no entries.
- Version prefix `11.0.0`, kept in step with the referenced Microsoft.Extensions.Hosting version (`serilog-serilog-extensions-hosting:Directory.Version.props@d69d6f1#L3-L4`).
- Tracked branches: `dev`, and `release/10.0` as the newest match for `release/*` (see the component's `branches`).

## Open questions

None on the repository itself. The component has one.
