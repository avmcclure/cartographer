# Using Cartographer

Cartographer maps your organisation's repositories into an **atlas**: a git repository of Markdown entries describing each repository, the components it ships, your databases, and how they all link together. A command-line tool reads what can be read mechanically. An AI coding agent, following Cartographer's skills, adds what needs judgement.

You need three things: the `cartographer` CLI, the skills installed in your agent (Claude Code or GitHub Copilot), and an atlas.

## 1. Install the CLI

The CLI is a .NET tool. You need the .NET 10 SDK and git.

Until a package is published to a feed, build it from a clone of this repository.

```
git clone https://github.com/avmcclure/cartographer
cd cartographer
dotnet pack src/Cartographer.Cli -c Release -o nupkg
dotnet tool install --global Cartographer.Cli --add-source nupkg
```

Check it: `cartographer` with no arguments lists its commands.

Cartographer stores no credentials. It uses the sign-ins you already have:

- **GitHub**: `gh auth login`, or a `GITHUB_TOKEN` environment variable.
- **Azure DevOps**: `az login`, or an `AZURE_DEVOPS_EXT_PAT` environment variable.
- **Cloning**: git's own credential helper.
- **SQL Server** (optional): integrated sign-in, or a connection string in an environment variable named `CARTOGRAPHER_SQL_<INSTANCE>`.

## 2. Install the skills

There are four skills: `cartographer-setup`, `cartographer-scan`, `cartographer-questions` and `cartographer-ask`. They live in `skills/` in this repository, and this repository is also the plugin that ships them.

### Claude Code

```
claude plugin marketplace add avmcclure/cartographer
claude plugin install cartographer@cartographer
```

Or, inside a session, `/plugin marketplace add avmcclure/cartographer` and then `/plugin install cartographer@cartographer`. The skills appear as `/cartographer:cartographer-scan` and so on, and Claude also picks them up from what you ask.

To update later: `claude plugin marketplace update cartographer`, then `claude plugin update cartographer@cartographer`.

### GitHub Copilot CLI

```
copilot plugin install avmcclure/cartographer
```

Check with `/skills list` in a session. To update: `copilot plugin update cartographer`.

If you would rather not install a plugin, point Copilot at the skill folders in a clone: `/skills add <path to clone>/skills`. Or copy the four folders into `~/.copilot/skills/`.

### Other Copilot surfaces

Copilot in VS Code and the Copilot cloud agent read skills from `~/.copilot/skills/`, or from `.github/skills/` in the repository they are working in. Copying the four folders to one of those places should work there too. This has not been tried, and it means keeping a copy up to date by hand.

## 3. Create an atlas

In your agent, from the directory where the atlas should live, say:

> Set up a Cartographer atlas for Acme.

The setup skill asks you a few things and does the rest:

- your GitHub and Azure DevOps organisation names;
- which repositories to leave out (archives, sandboxes);
- the name prefixes your internal packages share, such as `Acme.*`;
- which SQL Server instances it may read schema from, if any. Production is read only if you say so for that instance. It never reads table data.

It creates the atlas, lists your repositories and goes through a short checklist. The settings end up in `cartographer.yml` at the atlas root, which you can edit by hand at any time. It leaves the atlas on a branch called `first-sweep`, where the first sweep carries on. Reading live databases is described in `docs/live-databases.md`.

Create an empty repository for the atlas on GitHub or Azure DevOps, add it as the atlas's git remote (`git remote add origin <url>`) and push `main`, so that scans can open pull requests.

## 4. Run the first sweep

The first sweep maps everything, so it is done in stages over several sessions. From the atlas directory, say:

> Run the first sweep. Three sub-agents at once, at most twenty repositories, local only.

The two caps are yours to set at the start of every session: how many sub-agents work at once, and the most repositories to cover. Start low.

What happens:

1. **A mechanical pass over every repository.** No AI. This can take a while and costs nothing but time.
2. **A pilot on one small project** you know well. Read the entries it produces. This tells you what a repository costs and whether the quality is what you want before you spend on the rest.
3. **One project at a time**, the most depended-on first. Each session stops at your cap; start another session to carry on. Progress is kept in the atlas, so a session can be interrupted and resumed, on any machine.
4. **A last pass** that proposes systems (business groupings) and business-data tags such as `customer` and `payment`.

A scan never stops to ask you anything. Whatever it cannot work out, it records as an open question in the atlas. It only reads your code: nothing from a scanned repository is run or built, and no secret is copied.

### Review it before publishing

Everything a sweep writes stays on a local branch of the atlas. Nothing reaches `main` except by a pull request you merge. Before publishing:

- **Build the site** and look around: `cartographer site build --out site` (see section 8).
- **Read a sample** of entries from each project, and the list of open questions in `_generated/open-questions.md`.
- **Read what changed**: each repository's scan is one commit, with a summary in `changes/`. Ending a session prints the `git log` command that lists them.
- **Correct what is wrong** by editing the entry. Put text you want kept exactly between `<!-- person -->` and `<!-- /person -->`. If you change a link, set its `basis` to `stated` and its `source` to `person:<your name>@<today's date>`. Later scans leave both alone.

When you are satisfied, say:

> End the session with a pull request.

That pushes the branch and opens one pull request for the whole sweep. Review and merge it as you would any other.

## 5. Later scans

Whenever you want the atlas brought up to date:

> Scan what is due. Four sub-agents, at most fifteen repositories, pull request at the end.

A repository is due when its default branch has new commits, when something it was waiting on has changed, when it was last scanned more than 90 days ago, or when an earlier scan of it was left unfinished. Only the components whose files changed are looked at again.

`cartographer repos due` shows what is due and why, without scanning anything.

## 6. Answer open questions

Open questions are things a scan could not determine from code: which provider a setting points at, what a database is really called, who owns something. Those marked `needs-person` are waiting for you. They are listed in `_generated/open-questions.md` and on the site.

To answer one, open the entry (on GitHub is fine), find the question under `## Open questions`, and type your answer in plain words after `Answer:`. Commit it.

```markdown
- Answer: That's Worldpay. Finance own the contract. Priya
```

Then, in your agent:

> Work the open questions.

The questions skill turns each answer into proper links and text, marked as stated by you so that no scan overwrites it, and removes the question. It also retries questions that code may now be able to answer.

You can add a question of your own to any entry in the same layout, with `Raised by: person`. Agents try those first and never dismiss them.

## 7. Ask the atlas

From the atlas directory, just ask:

> What uses the Acme.Data library?
> What would changing the Invoice table affect?
> Where does customer data live?

The ask skill reads a few small files and answers with how each fact is known: **extracted** (read mechanically from a file or database), **inferred** (an agent concluded it from reading code) or **stated** (a person said it). It also tells you about open questions that bear on the answer. It works on the plain files, so it needs no CLI and works on a fresh clone of the atlas.

With the CLI installed you can ask directly:

```
cartographer find invoice
cartographer show databases/billing/tables/dbo/invoice
cartographer used-by databases/billing/tables/dbo/invoice
cartographer impact databases/billing/tables/dbo/invoice
```

## 8. Build the site

```
cartographer site build --out site
```

This writes a static site with a map of clusters (parts grouped by how they talk to each other), a page per cluster, a searchable list of every entry, the open questions that need a person, and recent changes. The site is built with Astro, so building it needs Node.js 22 or later. Add `--edit-url <url>` to give each entry an edit link to your atlas's repository, which is the quickest way for people to answer open questions. Serve the `site` folder with any static web server inside your organisation. Build it from a scan branch to review a sweep, or from `main` for everyone else.

Clusters come from the links alone, so they appear without any setup. To keep one as it is, name it and pin it:

```
cartographer clusters --parts
cartographer clusters rename "order-api" "Ordering"
cartographer clusters pin --all
```

Renaming pins a cluster. Pins are kept in `clusters.yaml` in the atlas; commit it. When later scans change the links enough that regrouping would keep at least 3 percentage points more of them inside clusters, `cartographer clusters` and the map say so, and `cartographer clusters regroup` applies it.

An atlas made before clusters has a `layouts/` folder from the old plans. Delete it; nothing reads it any more.
