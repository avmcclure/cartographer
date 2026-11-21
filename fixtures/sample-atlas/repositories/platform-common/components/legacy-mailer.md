---
kind: component
name: Brackley.Mailer
type: library
summary: The COM component that used to send order confirmation mail. Its project was deleted; the entry is kept so links resolve.
status: retired
repository: repositories/platform-common/repository
stack:
  - vb6
sources:
  vbp: platform-common:legacy/Mailer/Mailer.vbp@8d41f06
provides:
  - name: Brackley.Mailer
    as: progid
    basis: extracted
    source: vbp#L3
verified:
  commit: "8d41f06"
  date: "2026-04-14"
  cartographer: "0.1.0"
---

## Overview

Sent the "we have your order" mail for the back office program. The project was removed from the repository in 2025. Nothing has been found that replaced it.

## Technical detail

<!-- extracted -->
Last seen at commit `8d41f06`: project `legacy/Mailer/Mailer.vbp`, an ActiveX DLL.
<!-- /extracted -->
