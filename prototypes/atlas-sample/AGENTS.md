# Atlas: how to navigate

This repository is an atlas written by Cartographer. It describes an organisation's repositories, what they ship, their databases and how they connect. Everything is plain Markdown with YAML frontmatter.

## Read in this order, and stop as soon as you have the answer

1. `_generated/index.md`: every top-level folder with counts, and the systems.
2. A folder index, e.g. `_generated/databases/cioc-primary/index.md`: one row per entry with ID, kind and summary.
3. One entry: its frontmatter `summary`, then `## Overview`.
4. Its `## Technical detail`, and the source file it points to, only if you still need more.

## Rules of the format

- An entry's ID is its path without `.md`, e.g. `databases/cioc-primary/tables/dbo/gbl_savedsearch`. Links use IDs.
- Links are stored only on the entry they start from (`links:` in frontmatter). To go the other way, read `_generated/<entry id>.used-by.md`.
- A link with `name:` and no `to:` is an unresolved reference: nothing in the atlas provides that name yet.
- `basis` says how a fact is known: `extracted` (read mechanically), `inferred` (an agent concluded it), `stated` (a person said so).
- `source` is `<repository-id>:<path>@<commit>#L<lines>`. The repository ID is the folder name under `repositories/`.
- Open questions are on the entry they concern; the full list is `_generated/open-questions.md`.

## If you edit

- Never change a field whose basis is `stated`, or text between `<!-- person -->` and `<!-- /person -->`.
- Never write a secret value. Note only that a credential exists at a path and commit.
- Do not edit anything under `_generated/` or any `scan-state.yml`; the CLI rewrites them.
