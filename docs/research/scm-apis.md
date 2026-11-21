# SCM APIs: listing repositories, detecting change, matching migrated repos

Research for issue #2. Checked against primary docs on 2026-11-20.

## Answer

- **Enumerate.** Both platforms list every repository cheaply. Azure DevOps (AzDO): one unpaginated call per organisation. GitHub: a paginated org listing, 100 per page.
- **Detect change.** GitHub's listing already carries `pushed_at`, so a few requests cover the whole org. AzDO's listing has **no last-push field**, so change detection costs one extra request per repository (branch tip SHA, or latest push). Store the default-branch tip SHA per repo and compare.
- **Match after migration.** GitHub records each GitHub Enterprise Importer (GEI) migration as a `RepositoryMigration` with `sourceUrl` and target `repositoryName`, queryable per organisation. That is the authoritative link. Shared commit SHAs are a fallback: GEI docs say commit history is migrated but do not state that SHAs are preserved, so treat it as an assumption to test on one migrated repo.
- **Auth.** GitHub App installation token; Microsoft Entra service principal for AzDO (PATs work but Microsoft discourages them).
- **Limits.** Not a concern at several hundred repos: a full sweep is a few hundred requests against 5,000+/hour on GitHub and a load-based (not request-count) throttle on AzDO. Run requests serially.

## (a) Enumerating repositories

**Azure DevOps.** `GET https://dev.azure.com/{organization}/_apis/git/repositories?api-version=7.1`. `project` is optional, so one call returns every project's repos. No paging parameters are documented. Each `GitRepository` has `id` (GUID), `name`, `project`, `defaultBranch`, `remoteUrl`, `size`, `isDisabled`, `isFork`. `includeHidden` defaults to false. Scope: `vso.code`. One call per AzDO organisation; an estate spanning several organisations needs the list of organisations supplied as config.
Source: https://learn.microsoft.com/en-us/rest/api/azure/devops/git/repositories/list?view=azure-devops-rest-7.1

**GitHub.** `GET /orgs/{org}/repos` with `per_page` max 100, `sort` of `created|updated|pushed|full_name`. Items include `pushed_at`, `updated_at`, `default_branch`, `archived`, `fork`, `size`.
Source: https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28

GraphQL equivalent: `Organization.repositories` (accepts `orderBy` with `PUSHED_AT`, `isArchived`, `isFork`); `Repository.pushedAt` is "the date and time when the repository was last pushed to". Connection arguments confirmed by schema introspection.
Source: https://docs.github.com/en/graphql/reference/repos

## (b) Detecting change cheaply

**GitHub.** Compare `pushed_at` from the listing with the stored value; sorting by `pushed` descending lets a scan stop at the first unchanged repo. For a per-repo check, `GET /repos/{owner}/{repo}/commits` accepts `sha` and `since`, and `GET /repos/{owner}/{repo}/compare/{basehead}` returns `status` (`identical|ahead|behind|diverged`) and `ahead_by`.
Source: https://docs.github.com/en/rest/commits/commits?apiVersion=2022-11-28

Conditional requests: send the stored `etag` as `if-none-match`; an authorised request answered with `304` "does not count against your primary rate limit".
Source: https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api?apiVersion=2022-11-28

**Azure DevOps.** `GitRepository` has no push or update timestamp (see field list above), so one request per repo is needed. Options:

- Tip SHA: `GET .../repositories/{id}/refs?filter=heads/{branch}` returns `name` and `objectId`; `filter=heads/` returns all branches, `$top` max 1000.
  Source: https://learn.microsoft.com/en-us/rest/api/azure/devops/git/refs/list?view=azure-devops-rest-7.1
- Since a time: `GET .../repositories/{id}/pushes` with `searchCriteria.fromDate`, `searchCriteria.refName`, `$top`; each push has `pushId` and `date`.
  Source: https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pushes/list?view=azure-devops-rest-7.1
- Since a commit: `GET .../repositories/{id}/commits` with `searchCriteria.fromDate`, or `itemVersion`/`compareVersion` to list commits between two versions.
  Source: https://learn.microsoft.com/en-us/rest/api/azure/devops/git/commits/get-commits?view=azure-devops-rest-7.1

## (c) Recognising the same repository on both platforms

**Migration metadata.** The GraphQL `RepositoryMigration` object has `sourceUrl`, `repositoryName` ("the target repository name"), `state`, `createdAt`, and `migrationSource` whose `type` enum includes `AZURE_DEVOPS`. `migrationLogUrl` "expires 1 day after migration completes".
Source: https://docs.github.com/en/graphql/reference/migrations

`Organization.repositoryMigrations(state, repositoryName, orderBy, first, after)` exists in the live schema (confirmed by introspection; not seen on a docs page). Not verified: how long migration records are retained, the exact form of `sourceUrl` for AzDO, and which token permissions can read the connection.

**Migration log.** Each migrated repo gets an issue titled "Migration Log" that includes "the source of the migration"; it needs Issues enabled on the target. The downloadable log lasts 24 hours.
Source: https://docs.github.com/en/migrations/using-github-enterprise-importer/completing-your-migration-with-github-enterprise-importer/accessing-your-migration-logs-for-github-enterprise-importer

**Shared history.** GEI migrates "Git source (including commit history)" plus pull requests; Git LFS objects are not migrated. The docs do not say SHAs are preserved. Target naming is chosen by the operator (`--target-repo`), so names alone are not a reliable key. GEI "doesn't support delta migrations", so after cut-over the two sides diverge and tip SHAs stop matching; an older shared SHA (for example the root commit) would still match if SHAs are preserved.
Sources: https://docs.github.com/en/migrations/using-github-enterprise-importer/migrating-from-azure-devops-to-github-enterprise-cloud/about-migrations-from-azure-devops-to-github-enterprise-cloud and https://docs.github.com/en/migrations/using-github-enterprise-importer/migrating-from-azure-devops-to-github-enterprise-cloud/migrating-repositories-from-azure-devops-to-github-enterprise-cloud

Not verified: whether GEI disables or marks the source AzDO repo (`isDisabled` exists but nothing ties it to migration). Repos migrated by plain `git push --mirror` leave no migration record at all.

## Auth and rate limits

**GitHub.** GitHub recommends a GitHub App for organisation use. REST primary limits: 5,000 requests/hour for a PAT; App installations start at 5,000 and gain 50/hour per repo above 20 (cap 12,500); 15,000 on Enterprise Cloud organisations. Secondary: 100 concurrent requests, 900 points/minute per REST endpoint (GET = 1 point); GitHub advises serial requests.
Sources: https://docs.github.com/en/rest/authentication/authenticating-to-the-rest-api?apiVersion=2022-11-28 and https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api?apiVersion=2022-11-28

GraphQL: 5,000 points/hour per user (10,000 for Enterprise Cloud installations), `first`/`last` max 100.
Source: https://docs.github.com/en/graphql/overview/rate-limits-and-query-limits-for-the-graphql-api

**Azure DevOps.** Microsoft recommends Entra service principals or managed identities for background services and says to avoid PATs in production; PATs remain supported.
Source: https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/authentication-guidance?view=azure-devops

Throttling is load-based: 200 Azure DevOps throughput units (TSTUs) per identity in a sliding five-minute window. Cost per call cannot be computed in advance. Over the limit, requests are delayed (still HTTP 200, with `Retry-After`) and eventually blocked with 429. Watch `X-RateLimit-Remaining` and honour `Retry-After`.
Source: https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rate-limits?view=azure-devops
