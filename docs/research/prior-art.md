# Prior art worth reusing or learning from

Research for [#6](https://github.com/avmcclure/cartographer/issues/6). Primary sources only, fetched 2026-11-20. "Not verified" means the owning docs did not say.

## Answer

No existing tool does most of what Cartographer proposes. Each covers one slice, and none writes an agent-readable, git-stored map inferred from several hundred undocumented repositories.

- **Borrow the vocabulary, not the platforms.** Use Backstage's kinds and relation pairs for repositories, libraries and databases; OpenLineage's dataset naming (`mssql://{host}:{port}` + `{database}.{schema}.{table}`) and Job/Dataset shape for data flows; Structurizr's "relationship = source, destination, description, technology".
- **Reuse deterministic extractors inside the CLI.** The `dotnet` CLI and NuGet files for package and project graphs; SQL Server catalog views for object dependencies. Agents should interpret this output, not rediscover it.
- **Ignore as dependencies:** Backstage, DataHub and Structurizr as running systems (servers, databases, hand-authored input), and DeepWiki (per-repo prose, hosted, no cross-repo model).

The closest overlap is DataHub on the database half: its SQL Server source already extracts tables, views, stored procedures and procedure-derived table and column lineage. It needs a live database connection and a Kafka/OpenSearch/MySQL stack, and knows nothing about repositories or libraries. If the organisation would run DataHub anyway, Cartographer's database scope shrinks to linking code to it.

## Summary

| Tool | Does | Entities / relationships | Verdict |
|---|---|---|---|
| Backstage catalog | Catalog of software from YAML descriptors | Component, API, Resource, System, Domain; 7 inverse relation pairs | **Borrow model.** Mature vocabulary; ingestion assumes humans write the YAML. |
| Structurizr / C4 | Architecture model as code, many views | Person, Software System, Container, Component; directed relationships | **Borrow model** (relationship shape, zoom levels). Hand-authored. |
| OpenLineage | Open spec for lineage events | Job, Run, Dataset + facets | **Borrow model** (naming, column lineage). Runtime-event oriented. |
| DataHub | Metadata platform with crawlers | Entities + aspects + URNs; `upstreamLineage` | **Ignore as dependency; borrow field names.** Heavy stack, data-only. |
| DeepWiki | LLM-generated wiki per repo | Pages; no documented entity model | **Ignore.** Not cross-repo, not git-stored. |
| .NET tooling | Lists package/project references | Project -> package (requested, resolved, transitive); project -> project | **Reuse** in the CLI. |

## Detail

### Backstage software catalog

- Kinds: Component, API, Resource (core); System, Domain (ecosystem). [system-model](https://backstage.io/docs/features/software-catalog/system-model)
- Relations come in inverse pairs: `ownedBy/ownerOf`, `providesApi/apiProvidedBy`, `consumesApi/apiConsumedBy`, `dependsOn/dependencyOf`, `parentOf/childOf`, `memberOf/hasMember`, `partOf/hasPart`. None models tables, foreign keys or data flow. [well-known-relations](https://backstage.io/docs/features/software-catalog/well-known-relations)
- Envelope is `apiVersion`, `kind`, `metadata`, `spec`. `library` is a well-known Component type; a database is a Resource with free-form `type: database`. References look like `group:default/dev.infra`. [descriptor-format](https://backstage.io/docs/features/software-catalog/descriptor-format)
- Entities are read from YAML and stored in the catalog's database with a relations table; git is the source, not the store. [life-of-an-entity](https://backstage.io/docs/features/software-catalog/life-of-an-entity)
- Azure DevOps discovery only finds existing `catalog-info.yaml` files, and depends on the Code Search extension. [azure discovery](https://backstage.io/docs/integrations/azure/discovery)

Cartographer could emit `catalog-info.yaml` later as an export; that is cheap if the kinds and relation names already match.

### Structurizr / C4

- Hierarchy: software system > container (applications and data stores) > component > code. A database is a container. [c4model.com/abstractions](https://c4model.com/abstractions)
- DSL relationship: `<identifier> -> <identifier> [description] [technology] [tags]`, unidirectional. Elements carry tags and name/value properties. One `model` block, many views. `workspace extends` and `!include` share elements across workspaces. [DSL language](https://docs.structurizr.com/dsl/language), [workspace extension](https://docs.structurizr.com/dsl/cookbook/workspace-extension/)
- `!docs` attaches Markdown to a workspace, software system or container. [DSL docs](https://docs.structurizr.com/dsl/docs)

C4's levels map directly onto progressive disclosure: landscape index, then system, then container.

### OpenLineage

- Job (process), Run (one execution), Dataset; all extensible with facets. Design-time `JobEvent` and `DatasetEvent` exist for static metadata without a Run. [object-model](https://openlineage.io/docs/spec/object-model)
- SQL Server naming: namespace `mssql://{host}:{port}`, name `{database}.{schema}.{table}`. [naming](https://openlineage.io/docs/spec/naming)
- Column lineage facet maps output columns to input fields with `DIRECT` or `INDIRECT` transformations. [column lineage facet](https://openlineage.io/docs/spec/facets/dataset-facets/column_lineage_facet)

A stored procedure or an application write path is a Job with input and output Datasets. That is the data-flow entry shape.

### DataHub

- Entities composed of aspects, identified by URNs such as `urn:li:dataset:(urn:li:dataPlatform:redshift,userdb.public.customer_table,PROD)`. Core entities include Dataset, Data Job, Data Flow. [metadata-model](https://docs.datahub.com/docs/metadata-modeling/metadata-model), [dataset entity](https://docs.datahub.com/docs/generated/metamodel/entities/dataset)
- Lineage is the `upstreamLineage` aspect (with `fineGrainedLineages` for columns); foreign keys live in `schemaMetadata.foreignKeys`. [dataset entity](https://docs.datahub.com/docs/generated/metamodel/entities/dataset)
- The MSSQL source ingests tables, views, stored procedures (with code) and Agent jobs, with table- and column-level lineage from procedures and views. Trigger extraction: not verified. [mssql source](https://docs.datahub.com/docs/generated/ingestion/sources/mssql)
- Quickstart runs MySQL, OpenSearch, Kafka, GMS, frontend and actions containers; tested at 2 CPUs, 8 GB RAM. [quickstart](https://docs.datahub.com/docs/quickstart)

### DeepWiki-style docs

- Devin's DeepWiki indexes a repository and generates architecture diagrams, documentation and source links. It is configured per repo with `.devin/wiki.json`, capped at 30 pages (80 enterprise). Cross-repo relationships, storage location and export are not documented. [Devin docs](https://docs.devin.ai/work-with-devin/deepwiki)
- deepwiki-open is an MIT reimplementation for GitHub, GitLab and Bitbucket; Azure DevOps is not listed. [repo](https://github.com/AsyncFuncAI/deepwiki-open)

Lesson worth keeping: a committed steering file listing the pages wanted, with notes, replaces automatic planning.

### .NET and SQL Server dependency tooling

- `dotnet list package --include-transitive --format json` gives requested and resolved versions per target framework. It needs a restore (automatic from .NET 10). [dotnet package list](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list)
- `dotnet list reference` gives project-to-project references. [dotnet reference list](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-reference-list)
- .NET Framework projects still default to `packages.config`; restore needs every package source available. Opt-in `packages.lock.json` holds the full closure. [PackageReference](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- CycloneDX for .NET (Apache-2.0) reads solutions, projects and `packages.config` and emits an SBOM including transitives. [cyclonedx-dotnet](https://github.com/CycloneDX/cyclonedx-dotnet)
- `sys.sql_expression_dependencies` lists by-name dependencies for procedures, views, functions and T-SQL triggers, including cross-database and cross-server names (IDs unresolved). [Microsoft Learn](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-sql-expression-dependencies-transact-sql)

Consequence: restore-based tools will fail on repositories whose private feeds are unreachable from the scanner. The CLI needs a static fallback that parses `.csproj`, `packages.config` and `Directory.Packages.props` directly, with restore-based resolution as an upgrade when feeds are available.
