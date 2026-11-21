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

**Component**:
A thing a repository ships: a library, service, site, desktop app, job, ETL package or report. A repository has one or more.
_Avoid_: project, module, app, package

**Provided name**:
A name by which other code refers to a component: a package ID, assembly name, ProgID or type-library GUID.
_Avoid_: package, artifact

**Unresolved reference**:
A reference to a provided name that no known component provides.
_Avoid_: missing dependency, dangling link

**Database**:
A logical database identified by name, however many server instances it exists on.
_Avoid_: server, instance, catalog

**Routine**:
A stored procedure or function in a database.
_Avoid_: sproc, proc

**Trigger**:
Code a database runs by itself when a table's data changes. Has its own entry.
_Avoid_: hook, rule

**System**:
A business-level grouping of components and databases that together deliver one capability.
_Avoid_: product, application, domain

**Channel**:
A shared intermediary that components publish to or consume from: a queue, topic or file drop.
_Avoid_: bus, pipe, integration

**External system**:
A service or system outside the organisation that data flows to or from.
_Avoid_: third party, vendor, dependency

**Link**:
A typed relationship from one entry to another, recorded on the entry it starts from.
_Avoid_: edge, relation, reference

**Basis**:
How a fact was established: extracted (read mechanically from a file or catalog), inferred (concluded by an agent from reading code) or stated (said by a person).
_Avoid_: confidence, certainty, provenance

**Drift**:
A disagreement about the same database between two sources, such as a repository and a live instance.
_Avoid_: mismatch, divergence

**Flow**:
The path data takes across entries, found by following links. May also be narrated in an entry of its own.
_Avoid_: pipeline, lineage, journey

**Scan**:
One visit to a repository that creates or updates its entries.
_Avoid_: crawl, run, sync

**Open question**:
Something a scan could not determine, recorded in the atlas for a later scan or a person to answer.
_Avoid_: todo, unknown, gap
