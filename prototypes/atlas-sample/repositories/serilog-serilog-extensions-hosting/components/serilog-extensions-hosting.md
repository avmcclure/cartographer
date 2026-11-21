---
kind: component
type: library
summary: Library that routes Microsoft.Extensions.Hosting log messages through Serilog; depends on Serilog and Serilog.Extensions.Logging.
repository: repositories/serilog-serilog-extensions-hosting
provides:
  - {name: Serilog.Extensions.Hosting, as: nuget}
  - {name: Serilog.Extensions.Hosting, as: assembly}
links:
  - type: depends-on
    name: Serilog
    to: repositories/serilog-serilog/components/serilog
    version: "4.4.0"
    basis: extracted
    source: "serilog-serilog-extensions-hosting:src/Serilog.Extensions.Hosting/Serilog.Extensions.Hosting.csproj@d69d6f1#L32"
  - type: depends-on
    name: Serilog.Extensions.Logging
    version: "11.0.0"
    basis: extracted
    source: "serilog-serilog-extensions-hosting:src/Serilog.Extensions.Hosting/Serilog.Extensions.Hosting.csproj@d69d6f1#L38"
verified: {commit: d69d6f1, date: 2026-11-20, cartographer: 0.0.0-prototype}
questions:
  - id: q1
    state: open
    raised-by: cli
    missing: "Which component provides the package Serilog.Extensions.Logging. It matches the internal prefix Serilog.* but no scanned repository provides it."
    waiting-on: {kind: name, ref: "Serilog.Extensions.Logging"}
    answer-looks-like: "A component entry whose provides list contains {name: Serilog.Extensions.Logging, as: nuget}, or a person stating the package is public."
    attempts: 0
dismissed:
  - "Whether PolySharp is a run-time dependency: no. It is referenced with PrivateAssets=All, so it is build-time only (csproj L33)."
branches:
  "release/10.0": [{change: version, type: depends-on, name: Serilog, version: "4.3.0"}]
---

## Overview

Applications that use the standard .NET host add this library to send all their logging, including the framework's own messages, through Serilog. It is a thin adapter: it needs the core Serilog library and a second Serilog package, `Serilog.Extensions.Logging`, to do its work. The source of that second package has not been found yet, so a change there cannot currently be traced to this component.

## Technical detail

- Project: `serilog-serilog-extensions-hosting:src/Serilog.Extensions.Hosting/Serilog.Extensions.Hosting.csproj@d69d6f1`. No `PackageId` or `AssemblyName` is set, so both default to the project name. Root namespace is `Serilog` (L13).
- Target frameworks: `net462`, `netstandard2.0`, `netstandard2.1`, `net10.0`, `net11.0` (L8).
- `Serilog` is referenced at `4.4.0`; the Serilog component is at `4.4.1` on its default branch.
- `Serilog.Extensions.Logging` is used directly in code, for example `using Serilog.Extensions.Logging;` at `src/Serilog.Extensions.Hosting/SerilogServiceCollectionExtensions.cs#L18`.

Public packages referenced (not entries, not links):

| Package | Version | Condition |
|---|---|---|
| PolySharp | 1.14.1 | build-time only |
| Microsoft.Extensions.Hosting.Abstractions | 11.0.0 | all targets except `net11.0` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 11.0.0 | all targets except `net11.0` |
| Microsoft.Extensions.Logging.Abstractions | 11.0.0 | all targets except `net11.0` |

## Open questions

- **q1** (open, raised by the CLI, 0 attempts). Missing: which component provides the package `Serilog.Extensions.Logging`. Waiting on: the provided name `Serilog.Extensions.Logging` appearing in the atlas. An answer looks like: a component that provides it, or a person stating the package is public.
