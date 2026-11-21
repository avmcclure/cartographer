---
kind: repository
summary: Source of the CIOC Online Resources web application (classic ASP plus Python) and the SQL schema of its primary database.
links: []
verified: {commit: 7c461be, date: 2026-11-20, cartographer: 0.0.0-prototype}
questions:
  - id: q1
    state: open
    raised-by: agent
    missing: "Whether the Python code under python/cioc is a separate component (a Pyramid service) or part of the classic ASP site component."
    waiting-on: {kind: repository, ref: "opencioc-onlineresources"}
    answer-looks-like: "A component entry for the Python application with its own links, or a note on the site component saying the two are deployed as one."
    attempts: 0
---

## Overview

This repository holds the CIOC Online Resources software: the website that community information centres use to publish and search records of organisations and volunteer opportunities. It contains the web pages, a newer Python application that is gradually replacing them, and the definition of the main database the site runs on. Only the saved-search feature has been mapped in this sample.

## Technical detail

| Location | Identifier | Primary | Default branch |
|---|---|---|---|
| GitHub | `OpenCIOC/onlineresources` | yes | `master` |

- Components: [`components/online-resources-site`](components/online-resources-site.md), the classic ASP site (664 `.asp` files at this commit).
- Database source: `python/sql/core/` is the source for [`databases/cioc-primary`](../../databases/cioc-primary/database.md). It is a Red Gate SQL Source Control folder (`python/sql/core/RedGate.ssc`) with 481 table files, 987 stored procedure files, 313 function files and 12 view files.
- Not yet mapped: `python/cioc/` (169 `.py` files), `python/sql/coremigrations/`, `python/sql/datamove/`.
- The documentation says the move from classic ASP to Python on the Pyramid framework is not complete, and that a reverse proxy merges the two (`opencioc-onlineresources:docs/source/intro.rst@7c461be#L23-L36`).
- Tracked branches: `master` only. No branch matches `release/*`.
- Scan state: [`scan-state.yml`](scan-state.yml). The scan of this commit is incomplete.

## Open questions

- **q1** (open, raised by an agent, 0 attempts). Missing: whether the Python code under `python/cioc` is a separate component or part of the classic ASP site. Waiting on: the rest of this repository being scanned. An answer looks like: a component entry for the Python application, or a note on the site component saying the two are deployed as one.
