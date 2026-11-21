---
name: cartographer-scan
description: Runs a Cartographer scan session over an atlas, from start to finish, without stopping to ask anything. Use when the person asks to scan repositories, update or refresh the atlas, map what changed, run or continue the first sweep, or resume an interrupted scan. Needs the cartographer CLI and a directory containing cartographer.yml.
compatibility: Needs the cartographer CLI on the path, git, and a harness that can start sub-agents.
---

# Running a scan session

Cartographer keeps a map of an organisation's code in an **atlas**: a git repository of Markdown **entries**, one per repository, component, database, table and so on, joined by typed links. The `cartographer` CLI extracts what can be read mechanically. A **scan** of a repository adds what needs judgement, and that part is done by agents.

In a scan session you are the orchestrator. You run CLI commands and hand each repository to one fresh sub-agent. You do not read code or entries yourself. A session can cover dozens of repositories, and your context has to last to the end of it, so everything you take in should be a few lines: a command's result, a sub-agent's summary.

Run every command from the atlas root (the directory holding `cartographer.yml`). The CLI never prompts; exit code 0 is success, 1 a failed check, 2 a usage or environment error.

## Never stop to ask

A scan must be able to run to its cap with nobody present. Once it has started, ask the person nothing. Whatever you or a sub-agent would ask becomes an open question in the atlas, where it outlasts the session and reaches someone who can answer. If something fails, record it, move to the next repository, and report at the end.

## Settings

Take these from what the person said when starting. Where they said nothing, use the default and say in your first message which settings you are using. Do not ask.

| Setting | Default |
|---|---|
| Sub-agents at once | 3 |
| Most repositories this session | 10 |
| At the end | local only (no pull request) |
| Which repositories | whatever is due, in the order `repos due` gives |

If the person asked for the first sweep, or the atlas has no repository entries yet, read [references/first-sweep.md](references/first-sweep.md) and follow it; it uses the loop below in stages.

## The session

1. **Start.** `cartographer session start` creates the scan branch and prints its name. It refuses an atlas with uncommitted changes; if it does, report that and stop. Nothing a scan writes goes to the atlas's main branch directly.
2. **What is due.** `cartographer repos due` prints one line per repository, `<repository>: <reasons>`, then a count. Take the first ones up to the cap. If none are due, end the session and say so.
3. **Work in waves** of as many repositories as the sub-agent cap allows. For each wave:
   1. **Mechanical pass**, one repository after another, not in parallel: `cartographer scan <repository>`. It prints what it found and how many components are left to revisit, and exits 1 if the atlas no longer validates. Run it even for a repository whose earlier scan was left unfinished; components already marked done stay done. If `scan` fails for a repository, note the last line of its output, drop that repository from the wave and carry on.
   2. **Checkout path.** `cartographer clone <repository>` prints where the code is.
   3. **One fresh sub-agent per repository**, all of the wave at once. Give each the prompt below and nothing more. A fresh context per repository keeps one repository's code from colouring another's entries.
   4. **Read the summaries** when they return. Each is a short block with a `status`. Do not open the entries to check them; the next step does that.
   5. **Validate.** `cartographer check` prints one line per problem, `error: <entry id>: <message>` or `warning: …`, and exits 1 if there are errors. Warnings do not block. For each repository with errors, start one fresh sub-agent with the same prompt plus a `Fix:` section holding those error lines. Then run `check` once more.
   6. **Finish** each repository whose summary said `complete` and which has no errors: `cartographer finish <repository>`. It writes the change summary, rebuilds the indexes and commits that repository's files on the scan branch, one commit per repository. `finish` does not check that every component is done, so never run it for a repository that is `partial`, `blocked` or still has errors.
   7. **Keep partial work.** If the wave left any repository unfinished, run `cartographer session commit --message "Partial scan of <repositories>"`. That commits their progress, so a later session, on any machine, resumes them; they stay due.
4. **Stop** when the repository cap is reached, nothing is due, or your own context is running low. Do not start a wave you cannot see through.
5. **End.** `cartographer session end` keeps the branch local and prints how to review it. `cartographer session end --pull-request` pushes it and opens a pull request; use it only if the person chose that at the start. It needs the atlas to have a git remote; if it fails, end local-only and say why.
6. **Report** in a few lines: repositories completed, those left partial or blocked and why, questions raised, how many now wait on a person, and the branch name or pull request.

## The sub-agent prompt

The instructions for the sub-agent are in `references/repository-agent.md`, next to this file. Do not read that file yourself and do not paste it into the prompt; give its absolute path and let the sub-agent read it in its own context.

```
You are completing the Cartographer atlas entries for one repository.
Read <absolute path>/references/repository-agent.md in full, then follow it exactly.

Atlas: <absolute path of the atlas>
Repository: <repository id>
Checkout: <path printed by cartographer clone>

Reply with only the summary block that file defines.
```

Use a general-purpose sub-agent that can read files, edit files and run shell commands. If your harness cannot start sub-agents, work the repositories yourself one at a time by following `references/repository-agent.md`, cover fewer of them, and say in the report that you did.

## References

- [references/repository-agent.md](references/repository-agent.md): what each sub-agent does. For sub-agents.
- [references/entry-format.md](references/entry-format.md): the entry format with two worked examples. For sub-agents.
- [references/first-sweep.md](references/first-sweep.md): the staged procedure for mapping an organisation for the first time.
