# The first sweep

The first time an organisation is mapped, every repository is due at once: often several hundred. A first sweep is the same scan session repeated, in an order that keeps cost known and results reviewable. It takes many sessions. All progress is in the atlas, so each session picks up where the last one stopped.

Use one scan branch for the whole sweep, and run every session local-only until the person says to publish. Start each session with:

```
cartographer session start --branch first-sweep
```

The setup skill creates this branch; when it already exists the command switches to it. If `cartographer repos list` says no repositories are known yet, run `cartographer repos list --refresh` once, after starting the session.

The person reviews locally as the sweep goes (see the end of this file) and publishes one large pull request when it is done, unless they ask for one per stage.

Work out which stage the sweep is at from the atlas, not from memory: `cartographer repos due` and the root index tell you.

## Stage 1: the mechanical pass over everything

No agents. The tool lists, clones and extracts from every repository.

```
cartographer scan --all
cartographer check
```

`scan --all` can take a long time and can be interrupted; run it again and it carries on with what is still due. Use `--max <n>` to do it in pieces.

Why this comes first: a consumer's reference to a package can only be recognised as internal once the repository that provides that name has been read. Stage 1 builds the index of every provided name across the estate, so that by the time agents read a consumer, its `depends-on` links already point at the right components.

When `check` passes, commit the result on the scan branch, so that the mechanical pass is one reviewable unit holding only extracted facts:

```
cartographer session commit --message "Mechanical pass over every repository"
```

Every repository now has a `scan-state.yml` with its components pending, so every repository is still due: it is waiting for an agent.

Tell the person the counts (repositories, components, databases, CLI questions) from `_generated/index.md` and the first lines of `_generated/open-questions.md`.

## The pilot

Before spending on the whole estate, run stage 2 on one small Azure DevOps project that somebody knows well. If the person named one, use it; otherwise choose the project with the fewest repositories that still has at least one library and one consumer.

Run an ordinary session limited to that project's repositories (their folders start with `<project>-`), with a low cap on agents at once. At the end, report to the person:

- how many repositories were completed, and how many needed more than one agent;
- how long it took and, if the harness shows it, what it cost, so that there is a figure per repository;
- how many open questions were raised per repository, and how many wait on a person;
- three or four entries worth reading as a sample.

Then stop. The pilot exists so that a person can read the entries, correct the skills or the config if the quality is off, and choose the caps for the rest. Do not carry on into the full sweep in the same session unless the person said to when they started it.

## Stage 2: one project at a time, most depended-on first

Each session takes one Azure DevOps project. A project is an area somebody can review, and its repositories tend to use each other.

**Order.** Do the projects the rest of the estate leans on first, so that when a consumer is read, the libraries it uses already have their entry-point lists. Rank projects by how many links arrive from outside them. The generated reverse lists hold this; count rather than read:

```
grep -h '^- repositories/' _generated/repositories/<project>-*/components/*.used-by.md | grep -vc '^- repositories/<project>-'
```

That prints, for one project, the number of links into it from outside it. Project names come from `cartographer repos list` (the middle part of an `azdo:` location). You need a rough order, not an exact one. Take the highest-ranked project that still has repositories due.

**Every repository gets an agent**, dormant ones included. Do not skip a repository because nothing has been committed to it for years: old code that still runs is what people most need mapped.

**Within the session**, follow the ordinary loop in `SKILL.md`, limited to that project's repositories. Stop at the cap; the project carries on next session.

When a project has nothing left due, tell the person it is ready to review and name the next one.

## Stage 3: systems and business-data tags

Systems and tags are left until the end because they need the whole picture: a system is a grouping across repositories and databases, which no single-repository agent can see. Until this stage the site shows Azure DevOps projects as provisional groupings.

Do this when no repository is due, or when the person asks for it. Keep your own context small here too: give the reading to sub-agents and read only their summaries.

**Systems.** For each Azure DevOps project, and then once for whatever is left unassigned, start one sub-agent with these instructions:

- Read `_generated/repositories/index.md` for the project's repositories, then the folder index of each, then only the summaries of their components. Follow `uses`, `reads`, `writes` and `calls` links to see which databases and components belong together.
- Propose systems: groupings that together deliver one business capability. A project may hold several systems, and a system may span projects.
- For each system, create `systems/<system>.md` with `kind: system`, a `name`, a one-line `summary` and an Overview in plain language.
- Record membership on the members, not on the system: add to each component and database a `systems` item with the system's ID, basis `inferred` and a `source` pointing at the evidence that ties it to the group, such as the configuration line naming a shared database.
- Never change a membership with basis `stated`. Something that fits nowhere stays unassigned, with no question raised.
- Return the list of systems created and how many members each has.

**Tags.** For each database, start one sub-agent with these instructions:

- Read the database's table index under `_generated/databases/<database>/tables/`, and open a table's entry only when its name and summary do not settle what it holds.
- Add business-data tags to tables and views that hold a recognisable kind of business data: `customer`, `payment`, `order`, `employee` and so on. Each tag has basis `inferred` and a `source` citing the table's own definition. Use the same word for the same thing across databases; read `systems/` summaries for the organisation's own vocabulary.
- Do not tag lookup, logging or plumbing tables. Do not change a tag with basis `stated`.
- Return the tags used and how many tables got each.

The rules in `repository-agent.md` about people's facts, secrets, repository text and never asking apply to these sub-agents too; give them that file's path and tell them to read its section "The rules, and why".

Then commit. The command validates and rebuilds the indexes first; if it reports errors, give the error lines to a fresh sub-agent to fix and run it again.

```
cartographer session commit --message "Propose systems and business-data tags"
```

## Review and publishing

Nothing reaches the atlas's main branch except by a merged pull request. During the sweep everything is on the local scan branch, where the person can:

- build the site from it (`cartographer site build --out <dir>`, which needs Node.js) and look around;
- read a sample of entries per project and the list of questions that need a person (`_generated/open-questions.md`);
- correct entries by hand. Corrections marked as theirs stick: later scans leave them alone.

When the person says to publish, run `cartographer session end --pull-request`. Until then, end each session with `cartographer session end`.
