# Before the first sweep

The first sweep reads every repository and can run for days across many sessions. Each check here is cheap now and expensive to discover halfway through. Go through them with the person; say plainly which pass and which do not.

## The listing is right

- `cartographer repos list` shows about as many repositories as the person expects. A much smaller number usually means their sign-in cannot see everything; a much larger one means exclusions are missing.
- Repositories hosted at both SCMs show as one repository with two locations, not as two repositories. Unlinked pairs are fixed with `locations` in `cartographer.yml`.
- The excluded names are the ones the person meant.

## Code can be fetched

- Pick one small repository and run `cartographer clone <repository>`. It uses git's own credential helper. If it fails, the person fixes their git sign-in for that SCM; nothing in Cartographer stores credentials.
- Do the same for one repository at the other SCM, if there are two.
- There is room on disk. Clones are partial and kept in a cache outside the atlas between scans; several hundred repositories still add up. `cartographer cache clear` empties it.

## Internal names are covered

- `internal-prefixes` holds the organisation's package name prefixes, exactly. See `config.md` for why.

## Databases, if any are listed

- Every instance has its `environment` and `production` set truthfully, and a production instance has `allowed: true` only because the person said so for that instance.
- The person can reach each allowed instance from this machine. `cartographer db read <instance>` tries it and writes what it reads into the atlas; follow it with `cartographer session commit --message "Read <instance>"`. A failure is recorded with its reason and does not stop a scan.
- If a dedicated login is preferred, the DBA has seen `cartographer db role-script` and `cartographer db queries`.

## The atlas has somewhere to go

- The configuration is committed on the `first-sweep` branch (`git status` shows nothing to commit).
- If the sweep is to end in a pull request, the atlas has a git remote the person can push to. Without one, sessions still work local-only.

## The person knows what will happen

Tell them, briefly:

- The sweep is staged: a mechanical pass over everything with no agents; then a pilot on one project so the cost per repository is known; then agents one project at a time; then a last pass proposing systems and business-data tags.
- Scans only read. Nothing in a scanned repository is run, restored or built, and no secret is copied into the atlas.
- A scan never stops to ask. What it cannot work out becomes an open question in the atlas for a person to answer later.
- Results stay on a local branch until they choose to publish. They can build the site from that branch and correct entries first.
- Before each session they set two caps: how many sub-agents work at once, and the most repositories to cover.

## Choose a pilot

Ask which Azure DevOps project should be the pilot: small, and known well by someone who can judge the entries. If they have no preference, the scan skill picks the smallest project that has both a library and something that uses it.
