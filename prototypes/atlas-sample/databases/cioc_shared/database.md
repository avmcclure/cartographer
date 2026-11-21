---
kind: database
summary: Shared library database (cioc_shared) of SQL helper functions, error texts and reference data used by CIOC application databases.
sources: ["opencioc-ciocshared:@6b4fdf3"]
instances: []
system: {id: systems/cioc-online-resources, basis: inferred}
links: []
verified: {commit: 6b4fdf3, date: 2026-11-20, cartographer: 0.0.0-prototype}
questions:
  - id: q1
    state: needs-person
    raised-by: agent
    missing: "Which other applications use this database. Its README names 'the Feature Database' and 'the Community Taxonomy Manager'; neither matches a repository in the atlas."
    waiting-on: {kind: person}
    answer-looks-like: "The repositories (or external systems) behind those two names, or a statement that they are out of scope."
    attempts: 1
---

## Overview

A small database of shared building blocks. Application databases call into it to format dates, build links, look up translated names and turn error numbers into readable messages. One copy serves every CIOC application database on the same server, so a change here can affect all of them at once.

## Technical detail

- Source: the root of `repositories/opencioc-ciocshared`, a Red Gate SQL Source Control folder: 9 table files, 2 stored procedure files, 73 function files, 7 static data files, one CLR assembly script.
- No live instance is listed in the atlas config, so nothing here has been confirmed against a running database.
- Name: the source repository does not state the database name. `cioc_shared` is inferred from two things that agree: the Online Resources documentation calls the shared library database `cioc_shared` (`opencioc-onlineresources:docs/source/dbconfig.rst@7c461be#L30`), and routines there call objects defined in this repository by that name (for example `cioc_shared.dbo.sp_STP_UnknownErrorCheck`).
- Roles granted execute rights in the files read: `cioc_login_role`, `cioc_maintenance_role`.
- Mapped so far: table `shr_stp_error`; routines `sp_stp_unknownerrorcheck`, `fn_shr_stp_formaterror`.

## Open questions

- **q1** (needs a person, raised by an agent, 1 attempt). Missing: which other applications use this database; the README names "the Feature Database" and "the Community Taxonomy Manager". Waiting on: a person. An answer looks like: the repositories or external systems behind those names, or a statement that they are out of scope.
