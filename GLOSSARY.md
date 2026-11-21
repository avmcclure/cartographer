# Cartographer

Cartographer maps an organisation's repositories, dependencies and data flows into versioned flat files that agents and people can read.

## Language

**Atlas**:
The git repository holding one organisation's findings. Separate from the Cartographer tool; one per organisation.
_Avoid_: findings repo, docs repo, knowledge base

**Entry**:
One Markdown file in an atlas describing one thing, with structured frontmatter for its relationships and prose below.
_Avoid_: page, doc, node, finding file

**Repository**:
A logical codebase belonging to the organisation, independent of where it is hosted.
_Avoid_: repo copy, project

**Location**:
A place a repository is hosted on an SCM. A repository has one or more locations, e.g. during a migration.
_Avoid_: remote, mirror

**Scan**:
One visit to a repository that creates or updates its entries.
_Avoid_: crawl, run, sync

**Open question**:
Something a scan could not determine, recorded in the atlas for a later scan or a person to answer.
_Avoid_: todo, unknown, gap
