---
kind: repository
name: platform-common
summary: Shared libraries used across systems, among them the common .NET library and the retired COM mailer.
stack:
  - csharp
  - netstandard2.0
  - vb6
locations:
  - id: github:brackley/platform-common
    primary: true
verified:
  commit: "2f7c9e4"
  date: "2026-11-03"
  cartographer: "0.1.0"
---

## Overview

Code that belongs to no one system. The common library is used by every .NET service at Brackley. The mailer was a COM component for sending order confirmations; its project was deleted in 2025 and its entry is kept so that the links to it still resolve.

## Technical detail

<!-- extracted -->
| Files | Count |
|---|---|
| .cs | 58 |
| .csproj | 1 |
<!-- /extracted -->
