---
kind: component
name: Brackley.Common
type: library
summary: The shared .NET library for money, clocks and retry rules that every Brackley service builds on.
repository: repositories/platform-common/repository
stack:
  - csharp
  - netstandard2.0
sources:
  proj: platform-common:src/Brackley.Common/Brackley.Common.csproj@2f7c9e4
provides:
  - name: Brackley.Common
    as: nuget
    basis: extracted
    source: proj#L6
  - name: Brackley.Common
    as: assembly
    basis: extracted
    source: proj
verified:
  commit: "2f7c9e4"
  date: "2026-11-03"
  cartographer: "0.1.0"
---

## Overview

Small, widely used helpers: a money type that cannot lose pennies, a clock that tests can set, and the house retry rules. Nearly every .NET component at Brackley depends on it, in more than one version.

## Technical detail

<!-- extracted -->
Target framework: netstandard2.0. Package version 5.1.0.
<!-- /extracted -->
