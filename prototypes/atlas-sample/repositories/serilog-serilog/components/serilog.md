---
kind: component
type: library
summary: The Serilog logging library; provides the NuGet package and assembly named Serilog (version 4.4.1 at this commit).
repository: repositories/serilog-serilog
provides:
  - {name: Serilog, as: nuget}
  - {name: Serilog, as: assembly}
links: []
verified: {commit: 43105b3, date: 2026-11-20, cartographer: 0.0.0-prototype}
---

## Overview

Serilog is the library applications call to write log events with named, structured properties instead of plain text lines. It is a foundation piece: it depends on no other internal component, and other components depend on it. A change to its public surface can affect every consumer listed in its "used by" list.

## Technical detail

- Project: `serilog-serilog:src/Serilog/Serilog.csproj@43105b3`. No `PackageId` or `AssemblyName` is set, so both default to the project name, `Serilog`.
- Version prefix `4.4.1` (`serilog-serilog:Directory.Version.props@43105b3#L3`).
- Target frameworks: `net10.0`, `net9.0`, `net8.0`, `net6.0`, `netstandard2.0`, plus `net471` and `net462` when built on Windows (`Serilog.csproj#L8-L12`).
- Strong-named with `assets/Serilog.snk` (`serilog-serilog:Directory.Build.props@43105b3#L9-L10`). A signing key file exists at that path; its contents are not recorded here.

Public packages referenced (not entries, not links):

| Package | Version | Condition |
|---|---|---|
| PolySharp | 1.15.0 | all targets; build-time only (`PrivateAssets="All"`) |
| System.Diagnostics.DiagnosticSource | 8.0.1 | `net462`, `net471`, `netstandard2.0` |
| System.Threading.Channels | 8.0.0 | `net462`, `net471`, `netstandard2.0` |
| System.ValueTuple | 4.5.0 | `net462` |

Used by: see `_generated/repositories/serilog-serilog/components/serilog.used-by.md` (not written in this sample).

## Open questions

None.
