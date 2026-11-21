# Live databases

`cartographer db read <instance>` reads schema metadata from a live SQL Server instance into the atlas. It is optional: with no instance listed, database entries are built from repositories alone.

## Listing an instance

Cartographer connects only to instances a person has listed in `cartographer.yml`. A server name found in code or a config file is never connected to.

```yaml
instances:
  - name: sql-test-01              # the name used on the command line and in entries
    server: sql-test-01.acme.local
    environment: test
    production: false
    allowed: true
    databases: [Billing]           # empty or left out: every user database the identity can open
```

- An instance with `production: true` is read only when `allowed: true` is also written out for it.
- An instance with `allowed: false` is never read.
- `master`, `model`, `msdb` and `tempdb` are never read.

Then:

```
cartographer db read sql-test-01
cartographer db read sql-test-01 --database Billing
```

## Connecting

There are two ways, and nothing is stored or prompted for.

1. **Integrated sign-in** (the default): the CLI connects to the instance's `server` as the person running it.
2. **A connection string in the environment**: set `CARTOGRAPHER_SQL_<INSTANCE>`, where `<INSTANCE>` is the instance name in upper case with every other character replaced by `_`. For `sql-test-01` that is `CARTOGRAPHER_SQL_SQL_TEST_01`.

Either way the connection always has `Application Name=Cartographer`, read-only intent, a 15 second connect timeout and a 30 second command timeout. The server's certificate is trusted without checking only when a supplied connection string says `TrustServerCertificate=true`.

## The role script

`cartographer db role-script` prints a T-SQL script for a DBA to review and run. It creates a login, and in each database a role `cartographer_reader` holding one grant: `VIEW DEFINITION`. Two extras are in the script commented out: `SELECT` on `sys.sql_expression_dependencies`, and the server-level `CONNECT ANY DATABASE` with `VIEW ANY DEFINITION` for teams that would rather not add a user to every database.

## What is read, and what is not

`cartographer db queries` prints every statement a read sends. They select from catalog views only:

- **Read:** databases the identity can open; tables, views, procedures, functions and triggers with their last-modified dates; columns with types and primary keys; foreign keys; trigger events; the definitions of views, routines and triggers; routine parameters.
- **Never read:** table data, row counts, sizes.
- **Not read in v1:** CLR routines, database-level (DDL) triggers, SQL Agent jobs, linked servers.

Definitions are parsed with the same parser used for `.sql` files in repositories, so a database read from a repository and from an instance gives the same entries. On every entry the instance is a source, `instance:<name>@<date>`.

Each read records on the database entry:

- under `instances`: the date and the **permission level** the read had. `limited`: the identity lacks database-wide `VIEW DEFINITION`, or some definitions came back empty. `definitions`: every definition was readable. `full`: the dependency catalog was readable too;
- in the extracted block: what could not be read, so that absence is not taken to mean none exist;
- as **drift**: a column that differs from the repository's (on the table), and objects that exist on only one side (on the database).

`databases/<database>/scan-state.yml` keeps, per instance, a fingerprint of object names and last-modified dates. When it has not changed, the next read rebuilds nothing and says so.

A database no repository defines gets full entries and one open question asking where its source lives.

An instance that cannot be reached, or a login that fails, is recorded as a failed read with a short reason on the databases already known for that instance. The command exits 1 and nothing learned earlier is removed.

## Running the test container

One test in `tests/Cartographer.Tests/LiveDbTests.cs` reads a real server. It runs only when `CARTOGRAPHER_TEST_SQL` holds a connection string; otherwise it passes without doing anything. It loads `fixtures/live-db/*.sql`, which creates the databases `Shop` and `Ledger` and drops nothing, so point it only at a throwaway server.

```sh
SA_PASSWORD="$(openssl rand -base64 18)Aa1!"     # generated; never written to a file
docker run -d --name cartographer-sql --platform linux/amd64 \
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$SA_PASSWORD" \
  -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest

# wait until the log says "SQL Server is now ready for client connections", then:
CARTOGRAPHER_TEST_SQL="Server=localhost,14333;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=true" \
  dotnet test --filter A_real_server

docker rm -f cartographer-sql
```

On an Apple-silicon Mac the image runs under Rosetta emulation, which Docker Desktop must have enabled.
