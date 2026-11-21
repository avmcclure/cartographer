---
kind: repository
summary: Source of the Serilog structured-logging library for .NET; ships one NuGet package, Serilog.
links: []
verified: {commit: 43105b3, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

This repository holds Serilog, a logging library that other .NET applications and libraries build on. It ships one thing: the `Serilog` package. Anything in the organisation that writes structured logs through Serilog depends on what is built here.

## Technical detail

| Location | Identifier | Primary | Default branch |
|---|---|---|---|
| GitHub | `serilog/serilog` | yes | `dev` |

- Components: [`components/serilog`](components/serilog.md), built from `src/Serilog/Serilog.csproj`. It is the only project under `src/`.
- Projects under `test/` are not shipped and have no entries.
- Version for the whole repository is set in `Directory.Version.props` (`serilog-serilog:Directory.Version.props@43105b3#L3`).
- Tracked branches: `dev` only. No branch matches the atlas pattern `release/*`.

## Open questions

None.
