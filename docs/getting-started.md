# Start a Moongate server

This is the one first-start sequence for Moongate. It applies whether you installed
the release with [the Linux installer](installation.md), run the
[container image](docker.md), or build from source. Moongate is under active
development: characters enter the world, walk and see each other, and NPCs and
items run Lua scripts, but combat, a built-in NPC AI and most gameplay are not
implemented yet. See [Implementation status](implementation-status.md).

A server start needs a root, readable client files, the active role's PostgreSQL
database and reviewed SQL, and a private Redis instance for realm leases and
one-use handoff tickets. The steps below prepare these dependencies.

## Before you start

- Your own Ultima Online client data. Moongate does not distribute it. The initial
  packet protocol targets ClassicUO 7.x.
- A reachable PostgreSQL server on which you can create databases. The examples in
  this repository use PostgreSQL 16.
- A reachable Redis 7+ server with authentication and `maxmemory-policy noeviction`. Keep it on a private network.
- Two free TCP ports in standalone mode: login defaults to 2593 and game to 2595. The
  UDP ping server uses port 12000 when it is free.
- For a source build: Git and the .NET 10 SDK selected by `global.json`. Node.js is
  only needed to work on the documentation website.

## Where the commands live

The sequence uses three executables. Each installation method ships them:

| Command | Installed release | Source checkout |
| --- | --- | --- |
| Prepare a root | `mgctl init` (`mgboot` in releases 0.7 to 0.11) | `dotnet run --project src/Moongate.Server -- --initialize-root` |
| Server | `moongate` | `dotnet run --project src/Moongate.Server -c Release --` |
| Migrations | `mgctl migrate` | `dotnet run --project src/Moongate.Ctl -- migrate ... --migrations-directory ./migrations` |

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

Once the root is configured, `scripts/run_server.sh` does the build and the start in
one step: it publishes a Release build of the server and `mgctl`
into `dist/moongate`, runs `mgctl init` on the root to add the shipped files it lacks, and
starts the server on it:

```sh
scripts/run_server.sh --root-directory "$HOME/moongate"
```

`--skip-build` starts the build already in `dist/moongate`, `--build-only` publishes
without starting, and every other option goes to the server as it is, for example
`--pid-file-name game.pid`.

Without `--root-directory` the script uses `MOONGATE_ROOT`. Each build deletes
`dist/moongate` first, so keep nothing of your own in it. The script exports
`MOONGATE_ROOT` for a configuration that names its paths through `${MOONGATE_ROOT}`;
a `MOONGATE_ROOT` already set is kept as it is, also when `--root-directory` names
another root, so unset it or pass the same path.

## First start

1. **Prepare the root.** Give the server a directory of its own:

   ```sh
   sudo mkdir -p /srv/moongate && sudo chown "$USER" /srv/moongate
   mgctl init /srv/moongate
   ```

   This writes `config/moongate.toml` with the defaults, creates `logs/` and
   `plugins/`, copies the release's core SQL into `migrations/`, its shard data files
   into `data/`, its templates into `templates/` and its example scripts into
   `scripts/`. It needs no
   database and no client files. [Prepare a root with mgctl](mgctl.md) describes
   what happens on a root that already exists.

   To also enable optional administration with a self-signed TLS certificate, use:

   ```sh
   mgctl init /srv/moongate --generate-admin-certificate \
     --admin-certificate-hosts "login.example.test"
   ```

   Replace the example host with the DNS name or IP address your admin client uses;
   omit `--admin-certificate-hosts` for localhost only. This creates
   `certificates/admin.pfx` and public `certificates/admin.crt`, and explicitly
   updates four `[admin_api]` settings. The default bind stays `127.0.0.1:2590`.
   See [certificate setup](mgctl.md#generate-an-administration-certificate) for
   client trust, private-network access and reuse of an existing identity.

   `mgctl` ships in releases after 0.11.0; releases 0.7 to 0.11 have `mgboot <root>`
   instead. On 0.6.0, start the server once instead:
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

   UO client encryption is disabled by default. For a client that sends encrypted
   traffic, configure `[network.encryption]` using the
   [client encryption guide](server-configuration.md#uo-client-encryption) before
   starting the server. Apply the same profile to separate login and game processes.

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
   refuses to start while files are pending, so apply them first (or set
   [`persistence.auto_apply_migrations`](persistence-migrations.md#apply-at-startup) and let a
   start apply them):

   ```sh
   mgctl migrate apply --root-directory /srv/moongate --target auth
   mgctl migrate apply --root-directory /srv/moongate --target world
   ```

   `--target auth` uses `[persistence.accounts]`, `--target world` uses
   `[persistence.realm]`. The runner reads the root's configuration and its
   `migrations/` directory; `status` in place of `apply` lists pending files without
   applying them. Both targets have core migrations; `apply` reports each file it
   ran.

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
| `certificates/admin.pfx`, `certificates/admin.crt` | Optional `mgctl` administration TLS identity: private server PFX and public PEM for client trust |
| `migrations/auth/`, `migrations/world/` | Core SQL copied by `mgctl init`; plugins ship their own under `plugins/` |
| `data/` | Shard data files copied by `mgctl`, read at game and standalone startup; see [Shard data files](data-files.md) |
| `templates/items/`, `loots/`, `mobiles/`, `npc_lists/`, `spawns/`, `decorations/`, `gumps/` | [Templates](templates.md) copied by `mgctl`, loaded at game and standalone startup |
| `logs/moongate-*.clef` | Structured JSON log events, one per line |
| `logs/errors/<id>.md` | The report of each exception the server logged, ready to paste into a GitHub issue |
| `plugins/` | One assembly bundle per plugin directory |
| `scripts/` | Lua source: `init.lua`, the [mobile scripts](scripting/mobile-scripts.md) `mobiles/<script_id>.lua`, the [item scripts](scripting/item-scripts.md) `items/<script_id>.lua`, the Lua modules they share in `common/`, the gump scripts `gumps/<id>.lua`, and the generated `definitions.lua` and `.luarc.json` |
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

## When something fails

The console shows an exception as one line, its message and its report, never the stack:

```text
12:04:31.552 ERR BankService                  | Opening the bank failed: the bank box is missing - details: /srv/moongate/logs/errors/7f3a9c21aa.md (paste it into a GitHub issue)
```

The report holds the Moongate version and codename, the system, the .NET runtime, the time, the
source, the level, the message and the whole exception, with its inner exceptions and stacks, in Markdown. Open it and
paste it into a [GitHub issue](https://github.com/moongate-community/moongate/issues/new). Its
name hashes the exception, so the same one logged again, say by a timer, reuses its report, which
describes the first time it was seen. After 500 reports no new one is written, so exceptions whose
messages vary cannot fill the disk; delete the old ones to make room. In Docker the path is the
one inside the container, under the mounted root. A wrapper such as an `AggregateException` shows
the message of what it wraps. The `.clef` logs keep the full exception of every event.

## Common startup problems

| Symptom | Check |
| --- | --- |
| Exits right after writing `config/moongate.toml` | Expected on a fresh root: `ultima_path` is still `ChangeMe`. Continue with step 2 |
| Client path error | Set `ultima.ultima_path` to readable, real client data |
| `Map ... needs map{n}.mul or map{n}LegacyMUL.uop, staidx{n}.mul and statics{n}.mul` | The client lacks that map. Use a complete client, or remove the map from `data/maps.toml` |
| `The Ultima path has neither MultiCollection.uop nor multi.idx and multi.mul` | The client directory is incomplete. Point `ultima.ultima_path` at a full client installation |
| `tiledata.mul not found in the Ultima path` | The client directory is incomplete or is not a client directory. Point `ultima.ultima_path` at a full client installation |
| TOML parse or validation error | Fix the named field; existing files are not silently replaced |
| `Postgres connection` failure | The database does not exist, the host is wrong, or the role cannot log in. Inside a container, `localhost` is the container itself |
| Connection variable missing | Export the PostgreSQL and Redis variables referenced by the active TOML sections |
| Redis connection failure | Check the private endpoint, credential, Redis health and `noeviction` policy |
| Pending or changed migrations | Run the migration runner `status` and `apply` for the named target (step 4), or turn on `persistence.auto_apply_migrations`. Never edit an applied file |
| PostgreSQL schema changes required | An entity needs DDL that no migration provides. Generate and review a versioned SQL file with `--persistence-schema generate`, then apply it with the runner while the server is stopped |
| Port binding failure | Check `network.listen_address`, port availability and interface addresses |
| Another instance detected | Check the PID and running process; use a separate root for another server |
| `... file ... not found` for a data file, such as `maps.toml` | The root has no `data/`. Run `mgctl` on the root again: it adds the missing files and keeps the others |
| `InvalidDataException` naming a data file | Fix the entry the message names; see the validation rules in [Shard data files](data-files.md) |
| Script startup error | Fix `scripts/init.lua`; inspect the script filename and line in the log |
