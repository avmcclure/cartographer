# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Astro with a custom design: content collections for the entries, our own layouts and components. Parts of Starlight (search, sidebar) may be borrowed where they fit. The Cartographer CLI (C#/.NET) prepares the site content from an atlas, including indexes and Mermaid diagrams as plain Markdown. Hosting is undecided.

## Users

Two audiences, treated as equal, each with its own front door onto the same entries:

- **Product people** weighing a change. They ask what is affected if something changes, where a kind of data lives and flows, and what each system does. They read plain language and do not read code.
- **Engineers** mid-task, about to change code. They look up what uses a library, table or routine, how sure each link is, and the evidence behind it.

A third reader, coding agents, uses the atlas files and the CLI directly, not the site.

Assumed, not confirmed: the site is read on a desktop browser inside the organisation.

## Product Purpose

Cartographer scans an organisation's repositories and databases and writes what it finds into an atlas: versioned Markdown entries describing repositories, components, databases, tables, routines, systems, channels and external systems, and the links between them. The site is the human view of one organisation's atlas. Success is a product person answering "what does this change touch?" and an engineer answering "what uses this, and how do we know?" without asking someone who has been there fifteen years.

## Positioning

Every link states its basis (extracted mechanically, inferred by an agent, or stated by a person) and points to the file and commit behind it. What is not known is shown as an open question, and people's corrections are never overwritten. It covers the old estate as well as the new: VB6, classic ASP, COM and legacy .NET alongside modern .NET and SQL Server.

## Operating Context

- First target: an older Microsoft shop with several hundred repositories, mid-migration from Azure DevOps to GitHub.
- The site is generated from the atlas after scans; scan results reach the atlas by pull request.
- People answer open questions by editing atlas files on GitHub or by pull request; the site lists questions that need a person, with edit links.
- The atlas records where credentials were found in old code (never their values), so the site is sensitive and internal.

## Capabilities and Constraints

- Entry kinds: repository, component, database, table/view, routine, system, channel, external system, optional flow.
- Each entry has a one-line summary, a plain-language Overview, Technical detail and Open questions, in that order.
- Product views required: impact of a change, where data lives and flows (from business-data tags on tables), system overview.
- Engineering views required: entries with typed links, basis markers, last-verified date, click-through to evidence, reverse "used by" lists, drift between database sources, long-unverified entries flagged.
- Scale: thousands of entries; a single database may hold thousands of tables and routines.
- No generator draws diagrams from frontmatter. The map groups parts by how they interact, not by who owns them (ADR 1): clusters come from the links, people can pin and rename them, and a regroup is suggested when the links drift. The drawing is laid out in the browser; nothing about positions is stored.
- Terminology is fixed in `GLOSSARY.md`.
- Undecided: hosting and access control; search implementation; whether the site is rebuilt on every atlas merge.

## Brand Commitments

The site carries Cartographer's own identity, with the organisation's name shown as the subject of the atlas. No logo, palette or voice exists yet.

## Evidence on Hand

- A prototype sample atlas built from real public repositories is in progress on the branch `prototype/atlas-sample` (wayfinder ticket #16). Use its entries as real content.
- Design decisions are recorded as resolution comments on the wayfinder tickets linked from issue #1.
- There are no screenshots, customers, testimonials or metrics. Do not invent any.

## Product Principles

1. Say how we know. A fact without its basis and source is not shown as a fact.
2. Show what is unknown. Open questions and unverified entries are part of the picture, not hidden.
3. Depth on demand. A reader gets the summary first and goes deeper only when they choose to.
4. One atlas, two readers. Product and engineering views are different doors onto the same entries, never separate content.
5. People's knowledge outranks the scan.
