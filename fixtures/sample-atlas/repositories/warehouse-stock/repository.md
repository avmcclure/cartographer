---
kind: repository
name: warehouse-stock
summary: The stock reservation service and the nightly stock sync package.
stack:
  - csharp
  - net8.0
  - ssis
locations:
  - id: github:brackley/warehouse-stock
    primary: true
verified:
  commit: "a83f6d1"
  date: "2026-11-05"
  cartographer: "0.1.0"
---

## Overview

Two things that keep stock figures right: a service that reserves stock as orders arrive, and an older Integration Services package that recounts overnight.

## Technical detail

<!-- extracted -->
| Files | Count |
|---|---|
| .cs | 97 |
| .csproj | 1 |
| .dtsx | 3 |
| .dtproj | 1 |
<!-- /extracted -->
