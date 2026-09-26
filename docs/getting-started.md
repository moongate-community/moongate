# Start a Moongate server

This is the one first-start sequence for Moongate. It applies whether you installed
the release with [the Linux installer](installation.md), run the
[container image](docker.md), or build from source. Moongate is under active
development: the transport, packet pipeline, scripting and persistence
infrastructure and login-to-game handoff are available, but character selection
and a playable world are not implemented yet. See [Implementation status](implementation-status.md).

A server start needs a root, readable client files, the active role's PostgreSQL
database and reviewed SQL, and a private Redis instance for realm leases and
one-use handoff tickets. The steps below prepare these dependencies.

## Before you start

- Your own Ultima Online client data. Moongate does not distribute it. The initial
  packet protocol targets ClassicUO 7.x.
- A reachable PostgreSQL server on which you can create databases. The examples in
  this repository use PostgreSQL 16.
- A reachable Redis 7+ server with authentication and `maxmemory-policy noeviction`. Keep it on a private network.
- Two free TCP ports in standalone mode: login defaults to 2593 and game to 2595.
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
`--entrypoint`; see [Run with Docker](docker.md).

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
   and `scripts/`, copies the release's core SQL into `migrations/` and its shard
   data files into `data/`. It needs no
   database and no client files. [Prepare a root with mgboot](mgboot.md) describes
   what happens on a root that already exists.

   To also enable optional administration with a self-signed TLS certificate, use:

   ```sh
   mgboot /srv/moongate --generate-admin-certificate \
     --admin-certificate-hosts "login.example.test"
   ```

   Replace the example host with the DNS name or IP address your admin client uses;
   omit `--admin-certificate-hosts` for localhost only. This creates
   `certificates/admin.pfx` and public `certificates/admin.crt`, and explicitly
   updates four `[admin_api]` settings. The default bind stays `127.0.0.1:2590`.
   See [certificate setup](mgboot.md#generate-an-administration-certificate) for
   client trust, private-network access and reuse of an existing identity.

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
   connection_string = "$MOONGATE_ACCOUNTS_DATABASE"

   [persistence.realm]
   connection_string = "$MOONGATE_REALM_DATABASE"

   [redis]
   connection_string = "$MOONGATE_REDIS_CONNECTION_STRING"
   handoff_secret = "$MOONGATE_HANDOFF_SECRET"
   ```

   Use an absolute client path; relative paths resolve from the process working
   directory, not from the root. Supply the two PostgreSQL URIs, the Redis
   connection string and a separate cluster-wide handoff secret through the
   referenced environment variables. The secret must be at least 32 UTF-8 bytes.
   Read credentials from Bitwarden; do not
   write their values in this file. Every login and game process in one deployment
   needs the same Redis endpoint, Redis password and handoff secret.
   The [configuration reference](server-configuration.md) lists every setting.

3. **Provision PostgreSQL and Redis.** Create the Accounts and World databases and
   role-specific credentials before starting Moongate. The server never creates
   databases or roles. Use a DML-only runtime role and a separate schema role;
   see [Separate DDL and runtime roles](persistence-operations.md#separate-ddl-and-runtime-roles).

   Provision Redis with a strong password, private-network access and
   `maxmemory-policy noeviction`. Keep its password separate from the handoff
   secret. For a runnable PostgreSQL and Redis topology, use the
   [Docker login and realms example](docker-login-realms.md). Standalone checks
   both databases; login checks Accounts only, and game checks its Realm only.
   All three modes also check Redis at startup.

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

   Always pass `--root-directory`. Without it, or with a root that is the directory the
   binary sits in, the server refuses to start with exit code 2, because an upgrade
   replaces that directory. A successful start logs `Postgres connection successful` once per
   database, then the loaded services and the bound endpoints. A missing
   `scripts/init.lua` is a warning and starts an empty scripting environment; a
   bootstrap script that exists but fails prevents startup. Add scripts with
   [Writing Lua scripts](scripting.md). The interactive console commands are listed
   in [Server commands](commands.md).

6. **Stop it.** Press Ctrl+C and let shutdown finish. After a successful startup the
   host runs a final world save before closing PostgreSQL persistence. A failed
   startup or a faulted game loop cannot promise that save. Do not terminate the
   process while it is waiting for one.

To run another standalone instance, give it its own root, distinct login and
game listener ports, a distinct `realm_directory.realm_id` and `server_index`,
and its own realm database. Reusing the default realm ID and index replaces the
first instance's Redis lease. Never point two servers at one root or at one
realm database.

## Files and process ownership

All server-managed paths below are relative to `--root-directory`:

| Path | Purpose |
| --- | --- |
| `config/moongate.toml` | Created if missing; normal startup preserves it, while explicit certificate setup updates four `[admin_api]` settings |
| `certificates/admin.pfx`, `certificates/admin.crt` | Optional `mgboot` administration TLS identity: private server PFX and public PEM for client trust |
| `migrations/auth/`, `migrations/world/` | Core SQL copied by `mgboot` (releases after 0.6.0); plugins ship their own under `plugins/` |
| `data/` | Shard data files copied by `mgboot`, read at game and standalone startup; see [Shard data files](data-files.md) |
| `templates/items/`, `templates/loots/`, `templates/mobiles/` | Created at game and standalone startup for [templates](templates.md); nothing reads them yet |
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
| `tiledata.mul not found in the Ultima path` | The client directory is incomplete or is not a client directory. Point `ultima.ultima_path` at a full client installation |
| TOML parse or validation error | Fix the named field; existing files are not silently replaced |
| `Postgres connection` failure | The database does not exist, the host is wrong, or the role cannot log in. Inside a container, `localhost` is the container itself |
| Connection variable missing | Export the PostgreSQL and Redis variables referenced by the active TOML sections |
| Redis connection failure | Check the private endpoint, credential, Redis health and `noeviction` policy |
| Pending or changed migrations | Run the migration runner `status` and `apply` for the named target (step 4). Never edit an applied file |
| PostgreSQL schema changes required | An entity needs DDL that no migration provides. Generate and review a versioned SQL file with `--persistence-schema generate`, then apply it with the runner while the server is stopped |
| Port binding failure | Check `network.listen_address`, port availability and interface addresses |
| Another instance detected | Check the PID and running process; use a separate root for another server |
| `... file ... not found` for a data file, such as `maps.toml` | The root has no `data/`. Run `mgboot` on the root again: it adds the missing files and keeps the others |
| `InvalidDataException` naming a data file | Fix the entry the message names; see the validation rules in [Shard data files](data-files.md) |
| Script startup error | Fix `scripts/init.lua`; inspect the script filename and line in the log |
