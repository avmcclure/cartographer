# Cartographer

Maps an organisation's repositories, databases and the links between them into an **atlas**: a git repository of Markdown entries that agents and people can both read, with a static site built from it: a map of parts grouped by how they talk to each other.

- A deterministic CLI (C#/.NET, never calls a model) reads what can be read mechanically: project files, config, VB6 and classic ASP, SQL Server schema from files and from live instances.
- Agent skills, run inside Claude Code or GitHub Copilot, add what needs judgement: plain-language descriptions, links found by reading code, systems, open questions.
- Every fact says how it is known: **extracted**, **inferred** or **stated** by a person. What could not be determined is an open question, retried on later scans.

Status: v1 is built and tested against fixtures and public repositories. It has not yet been run on a real organisation's estate.

## Start here

- **Using it:** [docs/using.md](docs/using.md). Live databases: [docs/live-databases.md](docs/live-databases.md).
- **What v1 is:** [docs/spec/v1.md](docs/spec/v1.md), the entry format, commands and boundary.
- **Words:** [GLOSSARY.md](GLOSSARY.md).

## Layout of this repository

| Path | What |
|---|---|
| `src/Cartographer` | The library: atlas core, extractors, scan sessions, SCM access, live database reading, site data and clusters. |
| `src/Cartographer.Cli` | The `cartographer` dotnet tool. |
| `site/` | The Astro site template the tool ships and builds. |
| `skills/`, `.claude-plugin/` | The four skills and the plugin that ships them. |
| `fixtures/` | Small invented repositories and the exact atlas each must produce; `sample-atlas` is a finished atlas. |
| `tests/` | `dotnet test`. Set `CARTOGRAPHER_ACCEPT=1` to rewrite expected atlases after an intended change, then review the diff. |

## Building

```
dotnet test
dotnet pack src/Cartographer.Cli -c Release -o out
dotnet tool install Cartographer.Cli --global --add-source out
```

Opt-in tests: `CARTOGRAPHER_LIVE_TESTS=1` lists and clones public repositories; `CARTOGRAPHER_TEST_SQL=<connection string>` reads a real SQL Server (see docs/live-databases.md for a throwaway container).
