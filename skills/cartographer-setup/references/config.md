# cartographer.yml

One file at the atlas root. `cartographer init` writes a starting version; edit it by hand. The CLI reads all its settings from this file, its arguments and the environment, and never prompts.

## Example

```yaml
organisation: Acme
github: {organisation: acme}
azure-devops: {organisation: acme}
exclude: ["*-archive", "sandbox-*"]
internal-prefixes: ["Acme.*"]
locations:
  repositories/finance-billing: ["azdo:acme/Finance/billing", "github:acme/Finance-billing"]
instances:
  - name: sql-test-01
    server: sql-test-01.acme.local
    environment: test
    production: false
    allowed: true
    databases: [Billing]
limits:
  scan-age-days: 90
  question-attempts: 3
```

## Keys, and what to ask

### `organisation`

The organisation's name, as people write it. Set by `init`.

### `github`, `azure-devops`

The organisation name at each SCM. One or both. Set by `init --github` and `init --azdo`. Scans cover every repository the signed-in person can see there, so a person with narrow access produces a narrow atlas: ask whether their access covers what they want mapped.

### `exclude`

Repository name patterns to leave out, with `*` as a wildcard. **Always ask.** Show the person the listing's totals and a sample of names, and ask what should not be mapped: archives, sandboxes, forks of public code, personal experiments, generated mirrors. Suggest patterns from names you can see; do not add one they have not agreed to. Disabled and empty repositories are skipped without a pattern.

Leaving something out is cheap to undo later. Mapping several hundred repositories nobody wanted is not.

### `internal-prefixes`

Name patterns for packages that are always the organisation's own, such as `Acme.*`. **Always ask**: "Do your internal packages share a name prefix?"

Why it matters: Cartographer decides that a referenced package is internal when some scanned repository provides that name. A package nobody provides is taken to be public and simply listed. But if its name matches one of these prefixes, it is kept as an unresolved reference with an open question instead, because it is probably an internal library whose repository is missing, excluded or not visible. Without prefixes, such a library would be filed quietly among public packages.

Be exact. A broad pattern such as `*.Core` would turn public packages into questions.

### `locations`

During a migration a repository can be hosted in two places. Cartographer links an Azure DevOps location to a GitHub location by itself when the GitHub name follows `<project>-<repository>` and the two share a commit. Use `locations` only for pairs it cannot link: a mapping here is a person's statement and wins.

After `repos list`, look for repositories that appear at only one SCM but look like a pair by name. Ask the person about those, and write one line for each pair they confirm:

```yaml
locations:
  repositories/<project>-<repo>: ["azdo:<org>/<project>/<repo>", "github:<org>/<name>"]
```

Location IDs are `github:<org>/<repo>` and `azdo:<org>/<project>/<repo>`.

### `instances`

Live SQL Server instances the CLI may read schema from. Optional: with none listed, databases are documented from repositories alone, and their entries say that no live source has confirmed them. **Always ask**, and leave the list empty if the person is unsure; it can be added later.

Cartographer connects only to instances listed here. It never connects to a server because code mentioned one. It reads schema only (tables, columns, keys, triggers, routines and their definitions), never table data.

For each instance, ask:

| Key | Ask |
|---|---|
| `name` | A short name for it, used in commands and evidence. |
| `server` | The server address. No user name or password: those never go in this file. |
| `environment` | `dev`, `test`, `staging`, `production` or the organisation's own word. |
| `production` | Is it production? `true` or `false`. |
| `allowed` | May Cartographer read it? For a production instance this must be an explicit yes from the person for that instance; otherwise write `false`. |
| `databases` | Which databases on it. An empty list means every database the connecting identity can see. |

The CLI connects as the person running it, using integrated sign-in. Where that does not work, the person sets a connection string in an environment variable named `CARTOGRAPHER_SQL_<INSTANCE>` in their own shell: the instance name in upper case with every other character replaced by `_`, so `CARTOGRAPHER_SQL_SQL_TEST_01` for `sql-test-01`. Do not ask to see it and do not write it anywhere.

Two commands help the person's DBA decide: `cartographer db queries` prints the exact catalog queries the CLI runs, and `cartographer db role-script` prints a script that creates a role with only the permissions those queries need.

### `limits`

Optional. Leave the defaults unless the person asks.

- `scan-age-days` (90): a repository not scanned for this long becomes due even if nothing changed.
- `question-attempts` (3): after this many failed attempts an open question is handed to a person.
