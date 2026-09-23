# Start a Moongate server

This is the one first-start sequence for Moongate. It applies whether you installed
the release with [the Linux installer](installation.md), run the
[container image](docker.md), or build from source. Moongate is under active
development: the transport, packet pipeline, scripting and persistence
infrastructure are available, but account login and a playable world are not
implemented yet. See [Implementation status](implementation-status.md).

A server start needs four things in place: a server root, a configuration that
points at your client files, two PostgreSQL databases, and the core SQL
migrations applied to them. The steps below produce them in that order.

## Before you start

- Your own Ultima Online client data. Moongate does not distribute it. The initial
  packet protocol targets ClassicUO 7.x.
- A reachable PostgreSQL server on which you can create databases. The examples in
  this repository use PostgreSQL 16.
- A free TCP port; the game listener defaults to 2593.
- For a source build: Git and the .NET 10 SDK selected by `global.json`. Node.js is
  only needed to work on the documentation website.

## Where the commands live

The sequence uses three executables. Each installation method ships them:

| Command | Installed release | Source checkout |
| --- | --- | --- |
| Prepare a root | `mgboot` (releases after 0.6.0) | `dotnet run --project src/Moongate.Server -- --initialize-root` |
| Server | `moongate` | `dotnet run --project src/Moongate.Server -c Release --` |
| Migration runner | `/opt/moongate/migration-runner/Moongate.MigrationRunner` | `dotnet run --project src/Moongate.MigrationRunner -- ... --migrations-directory ./migrations` |

The steps below use the installed names. Substitute the source-checkout form, keeping
everything after `--`. For the container image the same steps run through
`--entrypoint`; see [Run with Docker](docker.md#first-start).

For a source checkout, build once first:

```sh
git clone https://github.com/moongate-community/moongate.git
cd moongate
git switch develop
dotnet build Moongate.slnx -c Release
```

`develop` includes unreleased work. To reproduce a release, check out its tag and
use the documentation published for that version.

## First start

1. **Prepare the root.** Give the server a directory of its own:

   ```sh
   sudo mkdir -p /srv/moongate && sudo chown "$USER" /srv/moongate
   mgboot /srv/moongate
   ```

   This writes `config/moongate.toml` with the defaults, creates `logs/`, `plugins/`
   and `scripts/`, and copies the release's core SQL into `migrations/`. It needs no
   database and no client files. [Prepare a root with mgboot](mgboot.md) describes
   what happens on a root that already exists.

   `mgboot` ships in releases after 0.6.0. On 0.6.0, start the server once instead:
   it writes the configuration and exits. Nothing else is needed, because on 0.6.0
   both the server and the migration runner read the core SQL from
   `/opt/moongate/migrations`, beside the executable:

   ```sh
   moongate --root-directory /srv/moongate
   ```

2. **Edit the configuration.** Open `/srv/moongate/config/moongate.toml` and set the
   client path and both database connections. Keep the other generated sections:

   ```toml
   [ultima]
   ultima_path = "/absolute/path/to/your/ultima-client"

   [persistence.accounts]
   connection_string = "postgres://moongate:moongate@localhost:5432/auth"

   [persistence.realm]
   connection_string = "postgres://moongate:moongate@localhost:5432/world"
   ```

   Use an absolute client path; relative paths resolve from the process working
   directory, not from the root. The two connection strings shown are the generated
   defaults; change host, credentials and database names to match step 3. Outside
   local development, write `"$MOONGATE_ACCOUNTS_DATABASE"` and
   `"$MOONGATE_REALM_DATABASE"` instead and supply the URIs from your secret provider.
   The [configuration reference](server-configuration.md) lists every setting.

3. **Create the databases.** Moongate never creates databases or roles. With the
   default connection strings, run as a PostgreSQL superuser:

   ```sql
   CREATE ROLE moongate LOGIN PASSWORD 'moongate';
   CREATE DATABASE auth OWNER moongate;
   CREATE DATABASE world OWNER moongate;
   ```

   Standalone checks both databases at every start; login checks Accounts only,
   and game checks its Realm only. For a deployment,
   give the server a DML-only role and keep schema changes on a separate role; see
   [Separate DDL and runtime roles](persistence-operations.md#separate-ddl-and-runtime-roles).

4. **Apply the core migrations.** Startup validates the versioned SQL history and
   refuses to start while files are pending, so apply them first:

   ```sh
   /opt/moongate/migration-runner/Moongate.MigrationRunner apply --root-directory /srv/moongate --target auth
   /opt/moongate/migration-runner/Moongate.MigrationRunner apply --root-directory /srv/moongate --target world
   ```

   `--target auth` uses `[persistence.accounts]`, `--target world` uses
   `[persistence.realm]`. The runner reads the root's configuration and its
   `migrations/` directory; `status` in place of `apply` lists pending files without
   applying them. The world catalog has no core tables yet, so its `apply` reports
   nothing to do.

5. **Start the server.**

   ```sh
   moongate --root-directory /srv/moongate
   ```

   Always pass `--root-directory`. Without it the server uses the directory the binary
   sits in. A successful start logs `Postgres connection successful` once per
   database, then the loaded services and the bound endpoints. A missing
   `scripts/init.lua` is a warning and starts an empty scripting environment; a
   bootstrap script that exists but fails prevents startup. Add scripts with
   [Writing Lua scripts](scripting.md). The interactive console commands are listed
   in [Server commands](commands.md).

6. **Stop it.** Press Ctrl+C and let shutdown finish. After a successful startup the
   host runs a final world save before closing PostgreSQL persistence. A failed
   startup or a faulted game loop cannot promise that save. Do not terminate the
   process while it is waiting for one.

To run a second instance, give it its own root, its own listener port and its own
realm database. Never point two servers at one root or at one realm database.

## Files and process ownership

All server-managed paths below are relative to `--root-directory`:

| Path | Purpose |
| --- | --- |
| `config/moongate.toml` | Server configuration; created once, never rewritten |
| `migrations/auth/`, `migrations/world/` | Core SQL copied by `mgboot` (releases after 0.6.0); plugins ship their own under `plugins/` |
| `logs/moongate-*.clef` | Structured JSON log events, one per line |
| `plugins/` | One assembly bundle per plugin directory |
| `scripts/` | Lua source and generated editor definitions |
| `moongate.pid` | Current process identifier |
| `moongate.pid.lock` | Lock file used to exclude another instance |

The PID guard is acquired before configuration is loaded. A live PID or an
already-held lock rejects another start. A stale or malformed PID is replaced.
The guard checks process liveness, not executable identity, so a reused PID can
also reject startup. Investigate that process before changing the PID file.
Normal cleanup removes the PID file if it still belongs to this process; the
`.lock` file may remain after its handle is released. Its presence alone does
not mean the server is running.

Console logs show time, level, source and message. File logs roll daily and at
10 MiB, keeping up to 30 files. For metrics see [Diagnostics](diagnostics.md);
for schema operations and world saves see
[PostgreSQL persistence](persistence.md).

## Common startup problems

| Symptom | Check |
| --- | --- |
| Exits right after writing `config/moongate.toml` | Expected on a fresh root: `ultima_path` is still `ChangeMe`. Continue with step 2 |
| Client path error | Set `ultima.ultima_path` to readable, real client data |
| TOML parse or validation error | Fix the named field; existing files are not silently replaced |
| `Postgres connection` failure | The database does not exist, the host is wrong, or the role cannot log in. Inside a container, `localhost` is the container itself |
| Persistence variable missing | Export the PostgreSQL URI referenced by the target's `connection_string` |
| Pending or changed migrations | Run the migration runner `status` and `apply` for the named target (step 4). Never edit an applied file |
| PostgreSQL schema changes required | An entity needs DDL that no migration provides. Generate and review a versioned SQL file with `--persistence-schema generate`, then apply it with the runner while the server is stopped |
| Port binding failure | Check `network.listen_address`, port availability and interface addresses |
| Another instance detected | Check the PID and running process; use a separate root for another server |
| Script startup error | Fix `scripts/init.lua`; inspect the script filename and line in the log |
