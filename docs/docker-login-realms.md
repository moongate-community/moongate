# Docker example: one login and two game instances

The [Compose example](../examples/docker/login-realms/compose.yaml) builds local
Moongate images and runs three server processes plus one PostgreSQL 16 service.
PostgreSQL owns three databases: Accounts, Realm 1, and Realm 2. Each database has
separate schema and runtime roles.

**This remains a topology example, not a working login-to-realm flow.** `mode` is
metadata and does not select service composition. Shared account APIs, realm
registration/discovery, login handoff, and role-specific startup are not yet
implemented.

## Topology and credential boundaries

| Service | Database access | Host client endpoint | Image target |
| --- | --- | --- | --- |
| `login` | Accounts runtime only | `127.0.0.1:2593` | ordinary `final` |
| `game-1` | Realm 1 runtime only | `127.0.0.1:2595` | optional `sample-plugin` |
| `game-2` | Realm 2 runtime only | `127.0.0.1:2596` | ordinary `final` |
| `schema-preview` / `schema-apply` / `migration-status` | Realm 1 runtime and schema | none; one-shot profile | optional `sample-plugin` |

A game container does not receive Accounts credentials. Future login/account APIs
will own shared-account access. A standalone deployment may separately configure
both targets, but that is outside this example.

All services join the private `moongate` bridge. The three server data directories
and the PostgreSQL data directory are separate named volumes. Ultima Online client
files are mounted read-only at `/uo`. The internal API remains disabled and no API
port is published.

`game-1` and the schema jobs use the Dockerfile's explicit sample-plugin stage.
The entrypoint copies the bundled `SamplePlugin` into that container's plugin
directory. It calls `AddPersistenceWorld<GreetingNote>()` for `sample_greeter.notes` in Realm 1,
so the schema commands exercise a real plugin registration. The ordinary final
image stays the Dockerfile default and does not contain the sample bundle.

## Requirements

Use a current Docker Engine or Docker Desktop with Compose v2, your own readable
Ultima Online client directory, and a Bitwarden CLI session or another process
secret provider. The example is authenticated; the committed `.env.example`
contains paths, ports, database/role names, and secret variable names only.

From the repository root:

```sh
cd examples/docker/login-realms
cp .env.example .env
```

Set `UO_DATA_PATH` in `.env` to an absolute host directory. Adjust ports or
non-secret database/role names there if required. Keep `.env` free of passwords.

Export seven strong passwords into the current shell from Bitwarden. For example,
replace each illustrative item name with your own organization/item reference:

```sh
export MOONGATE_POSTGRES_ADMIN_PASSWORD="$(bw get password moongate-postgres-admin)"
export MOONGATE_ACCOUNTS_SCHEMA_PASSWORD="$(bw get password moongate-accounts-schema)"
export MOONGATE_ACCOUNTS_RUNTIME_PASSWORD="$(bw get password moongate-accounts-runtime)"
export MOONGATE_REALM_1_SCHEMA_PASSWORD="$(bw get password moongate-realm-1-schema)"
export MOONGATE_REALM_1_RUNTIME_PASSWORD="$(bw get password moongate-realm-1-runtime)"
export MOONGATE_REALM_2_SCHEMA_PASSWORD="$(bw get password moongate-realm-2-schema)"
export MOONGATE_REALM_2_RUNTIME_PASSWORD="$(bw get password moongate-realm-2-runtime)"
```

Do not put these values in `.env`, TOML, command history, or checked-in files.
Compose secrets mount each value as a file. The entrypoint percent-encodes the PostgreSQL URI components and constructs the
connection environment variable in memory for the process that needs it.

Validate without printing the rendered model, then build:

```sh
docker compose config --quiet
docker compose build login game-1 game-2 schema-preview schema-apply migration-status
```

Missing secret variables fail validation with their names. Use `--quiet` as shown:
the Compose model contains expanded secret inputs in an extension used for early
validation, so plain `docker compose config` can expose them to the terminal.

## Provisioning and privileges

On the first empty PostgreSQL volume, `postgres/init.sh` creates all six roles and
three databases. Schema roles own their database and plugin schemas. Runtime roles
receive database `CONNECT`, schema `USAGE`, table DML, and default table DML for
new tables. They receive no schema creation permission. Moongate does not create
these grants itself.

The initialization scripts run only when the PostgreSQL data directory is empty.
Changing a password, role, database name, or grant later requires an explicit
PostgreSQL administration change; recreating an application container does not
rerun database initialization. Never use `docker compose down --volumes` on data
you intend to retain.

All TOMLs keep `auto_sync_schema = false` and use one `connection_string` per
database. Runtime TOMLs reference `$MOONGATE_REALM_DATABASE` (or the Accounts
variable); `game-1-schema.toml` references `$MOONGATE_REALM_SCHEMA_DATABASE`.
Schema jobs receive only the schema-role secret; normal hosts receive only the
runtime-role secret. Each process builds its PostgreSQL URI in memory.

## Review and apply schema changes

The sample plugin ships its reviewed SQL and manifest in the image. Start
PostgreSQL and inspect the pending files:

```sh
docker compose up -d postgres
docker compose --profile schema run --rm migration-status
```

Stop the affected runtime before applying the files:

```sh
docker compose stop game-1
docker compose --profile schema run --rm schema-apply
docker compose --profile schema run --rm migration-status
docker compose --profile schema run --rm schema-preview
```

`schema-apply` invokes the separate DbUp runner for World. Status should report
zero pending migrations; FreeSql preview should report no schema changes. A repeat
apply executes nothing. The PostgreSQL initialization script grants the runtime
role SELECT on migration history, with no ability to change it.

The job currently targets Realm 1. For Auth or another realm, use its own schema
configuration, credential, target (`auth` or `world`) and plugin bundle. Never point
a realm job at another realm's database. No migration transaction spans databases.
For an ordinary image, override the entrypoint directly:

```sh
docker run --rm --entrypoint /app/migration-runner/Moongate.MigrationRunner \
  -v /srv/moongate/schema-job:/data --env MOONGATE_REALM_DATABASE \
  moongate:local apply --target world --root-directory /data
```

Build `moongate:local` from a revision containing the migration runner. The source Compose example builds that image
locally. Its wrapper constructs the schema URI from Compose secrets in memory.
For authoring new SQL, see [Generate, review and apply](persistence.md#generate-review-and-apply).
The advisory lock serializes migration jobs; it does not stop runtime queries.

## Start and operate the servers

```sh
docker compose up -d login game-1 game-2
docker compose ps
docker compose logs --tail 100 login game-1 game-2
```

Compose waits for PostgreSQL health before starting each process. Each normal
startup validates its registered schema with its runtime connection. The minimal
TOMLs are mounted read-only at `/data/config/moongate.toml`; edit a source file
and recreate that service to apply a change.

Each server's `/data` volume contains its PID file, logs, generated scripts and
plugins. PostgreSQL data lives only in `postgres-data`. The host-managed TOMLs,
client files, and secret-provider records are outside those volumes.

Stop or recreate one instance independently:

```sh
docker compose stop game-1
docker compose up -d --no-build --force-recreate game-1
```

`docker compose down` removes containers and the network but retains named
volumes. `docker compose down --volumes` permanently removes the example's server
and database volumes.

World saves are not PostgreSQL backups. Moongate does not include a database
backup or restore workflow; that policy belongs to the operator.

## Reproducible local smoke

From the repository root, this disposable gate uses a unique Compose project,
temporary volumes, and synthetic process-only passwords on its private test
network:

```sh
sh examples/docker/login-realms/smoke.sh
```

It verifies missing-input failure, builds the local images, previews and applies
the real sample schema, checks runtime SELECT/INSERT/UPDATE/DELETE and DDL denial,
and checks clean startup and shutdown logs. The Realm 1 passwords contain literal
text-`COPY` escape sequences, and both roles must authenticate over TCP before the
schema gate continues. These disposable values exist only in the process
environment; no plaintext credential file is created.

## Troubleshooting

- **Missing variable during Compose validation:** export the named password from
  Bitwarden, or set `UO_DATA_PATH` in `.env`. Do not satisfy it with a committed
  plaintext file.
- **PostgreSQL initialization change has no effect:** initialization is one-time.
  Apply the intended role/database change administratively or start a deliberately
  new empty test volume.
- **Normal startup reports schema changes:** stop the affected runtime and run the
  schema preview/apply profile with the same plugin image.
- **Runtime permission denied:** verify database `CONNECT`, schema `USAGE`, table
  DML, and the schema owner's default table privileges.
- **Login does not list realms:** realm discovery and login handoff are not
  implemented; Docker DNS and `mode` metadata do not create that application flow.
- **Container exits:** inspect `docker compose ps -a` and the affected service log.
  Correct the configuration or database error before recreating it.
