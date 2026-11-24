# Atlas of Brackley

This repository is a Cartographer atlas: a map of Brackley's repositories, components, databases and the links between them. Every file is an entry about one thing.

Brackley is an invented parts wholesaler. This atlas is written by hand as the sample that Cartographer's site and tests are built from, and as the reference for what a finished atlas looks like.

## Reading it with little context

1. Read `_generated/index.md`.
2. Open one folder index under `_generated/`.
3. Open one entry and read its frontmatter `summary` and its Overview.
4. Read its Technical detail, and the source files it points to, only if you need them.

An entry's ID is its path without `.md`. What uses an entry is in `_generated/<id>.used-by.md`.
With the `cartographer` CLI installed: `cartographer find <text>`, `show <id>`, `used-by <id>`, `impact <id>`.

## Rules

- Every link says how it is known: `extracted` (read mechanically), `inferred` (concluded by an agent) or `stated` (said by a person).
- Never change or remove a `stated` fact, or text between `<!-- person -->` markers.
- Never write a secret value into an entry.
- What you cannot determine goes in the entry's Open questions section, not in prose.
- Do not edit `_generated/`, `scan-state.yml` or `changes/`; the CLI writes them. Change `clusters.yaml` with `cartographer clusters`.
- Run `cartographer check` before committing.
