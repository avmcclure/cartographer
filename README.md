# Cartographer

An agent-first documentation framework and toolset. It crawls the repositories of an organisation (Azure DevOps, GitHub, or another SCM) and builds a graph of the dependencies and data flows within it.

Status: planning. Nothing is built yet; the design is being worked out in this repo's issues.

## The idea

- **Findings as flat files in git.** Versioned by git history, and the source for a generated static site.
- **Internal libraries.** Discover where each library is used and how data flows through it.
- **Databases.** Track constraints (foreign keys, triggers) and stored procedures.
- **Incremental discovery.** Remember when each repository was last visited, and scan only what changed since.
- **Open questions.** Keep a list of unanswered questions and try to answer them on later scans.
- **Progressive disclosure.** Everything is laid out so an agent can load as little context as possible.

## Audience

- Product people, to understand the system their decisions land in.
- Engineers, to write better code against it.
