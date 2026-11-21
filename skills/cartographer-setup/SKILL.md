---
name: cartographer-setup
description: Creates a new Cartographer atlas for an organisation and gets it ready for a first sweep. Runs cartographer init, fills in cartographer.yml (SCM organisations, exclusions, internal name prefixes, live database instances), makes the first repository listing and checks what must be in place before scanning. Use when the person wants to set up, create, start or configure an atlas, map a new organisation, or change an atlas's cartographer.yml.
compatibility: Needs the cartographer CLI on the path and git. Listing needs the person's existing GitHub or Azure DevOps sign-in.
---

# Setting up an atlas

Cartographer maps an organisation's repositories into an **atlas**: a git repository of Markdown entries describing repositories, components, databases and the links between them. There is one atlas per organisation, kept apart from the Cartographer tool. This skill creates one and prepares it for the first sweep.

Setting up is a conversation. Unlike the other Cartographer skills, this one should ask the person questions: the choices here decide what gets scanned and what may be connected to, and only they know the answers. Ask a few at a time, suggest a sensible default for each, and do not guess the ones marked "always ask" in the reference.

Two things you must not do:

- **Never handle a secret.** Do not ask for, read out or write down a token, password or connection string. Cartographer stores none; it uses sign-ins the person already has. If a sign-in is missing, tell them which command to run themselves.
- **Never mark a production database as allowed** unless the person says so in plain words for that instance.

## Steps

1. **Check the tools.** `command -v cartographer` and `git --version`. If the CLI is missing, point the person to the installation section of Cartographer's `docs/using.md` and stop.
2. **Ask the basics:** the organisation's name; its GitHub organisation, its Azure DevOps organisation, or both; and the directory to create the atlas in (suggest `<organisation>-atlas` beside the current directory).
3. **Create it.**
   ```
   cartographer init <dir> --organisation <name> [--github <org>] [--azdo <org>]
   ```
   This writes `cartographer.yml`, an `AGENTS.md` that tells any agent how to read the atlas, and makes the first commit on `main`. Run the remaining commands from inside `<dir>`.
4. **Start the branch the first sweep will use**, before changing anything: `cartographer session start --branch first-sweep`. Everything from here until the person publishes is kept on that branch. The command refuses an atlas with uncommitted changes, which is why it comes first.
5. **Check the sign-ins** the listing will use: `gh auth status` for GitHub, `az account show` for Azure DevOps. The CLI also accepts `GITHUB_TOKEN` and `AZURE_DEVOPS_EXT_PAT` from the environment. If one is missing, ask the person to sign in themselves (`gh auth login`, `az login`) and wait.
6. **List the repositories.** `cartographer repos list --refresh`. Show the person the totals, not the whole list: how many repositories, how many at each SCM, how many are linked across both, and any the CLI skipped as disabled or empty.
7. **Fill in `cartographer.yml`** with the person, using what the listing showed. [references/config.md](references/config.md) has every key, what to ask for each, and an example. After each round of edits run `cartographer repos list` again so they can see the effect of an exclusion or a location mapping.
8. **Go through the checklist** in [references/before-first-sweep.md](references/before-first-sweep.md). Fix what can be fixed now; tell the person plainly about anything left.
9. **Commit the configuration:** `cartographer session commit --message "Configure the atlas"`, then `cartographer session end`. If the person has made an empty repository for the atlas on their SCM, ask them to add it as the git remote (`git remote add origin <url>`) and push `main`; a session can only open a pull request if there is one.
10. **Say what comes next:** the first sweep, run with the `cartographer-scan` skill. Suggest a pilot project if they have not named one.

## References

- [references/config.md](references/config.md): every key in `cartographer.yml`, what to ask, and an example.
- [references/before-first-sweep.md](references/before-first-sweep.md): what to check before the first sweep, and why each check matters.
