# Server configuration

The host loads `<root>/config/moongate.toml` with `ConfigHelper.Load`. A missing
file and its parent directory are created with defaults. Existing files are
deserialized and validated without being rewritten; omitted properties keep
their model defaults. Invalid TOML, invalid settings and filesystem errors fail
startup. Changes take effect at the next start; there is no configuration reload.

## Complete default configuration

TOML keys use `snake_case`. Keep `mode` before the first table header:

```toml
mode = "standalone" # Runs login and game services together.

[shard]
shard_name = "Moongate"

[network]
login_port = 2593
game_port = 2595
listen_address = "0.0.0.0"
enable_ping_server = true # Reserved: currently not consumed by the host.

[redis]
connection_string = "$MOONGATE_REDIS_CONNECTION_STRING"
handoff_secret = "$MOONGATE_HANDOFF_SECRET"

[admin_api]
enabled = false
# Use "*" or "0.0.0.0" for all IPv4 interfaces (TLS required).
listen_address = "127.0.0.1"
port = 2590
session_lifetime_minutes = 30
max_receive_message_bytes = 65536
max_concurrent_calls = 64
allow_insecure_loopback = false
certificate_path = ""
certificate_password = ""

[ultima]
ultima_path = "ChangeMe" # Replace with your client data directory.

[persistence]
auto_sync_schema = false

[persistence.accounts]
connection_string = "postgres://moongate:moongate@localhost:5432/auth"

[persistence.realm]
connection_string = "postgres://moongate:moongate@localhost:5432/world"

[realm_directory]
realm_id = ""
name = ""
server_index = 0
advertised_address = ""
advertised_port = 0
minimum_account_type = "regular"
heartbeat_interval_seconds = 5
lease_duration_seconds = 15
max_realms = 128

[world_save]
enabled = true # Enables periodic saves; manual/final saves remain available.
interval_seconds = 300

[diagnostics]
enabled = true
interval_seconds = 5
log_metrics = false

[scripting]
bootstrap_file = "init.lua" # Relative to <root>/scripts.
max_instructions_per_resume = 150000
max_instructions_per_chunk = 10000000
hook_interval = 1000
write_definitions = true
max_string_length = 16777216

[localization]
language = "eng" # Reads <root>/data/messages/eng.toml.

[line_of_sight]
max_distance = 25 # Farthest cells along X or Y a point can see.
```

Only the databases for the active role must already exist and accept connections:
Accounts for `login`, Realm for `game`, and both for `standalone`.
The defaults use local development credentials `moongate` / `moongate`; an existing
configuration file is not rewritten. For deployment, set each `connection_string`
to a secret-provider environment reference such as `$MOONGATE_ACCOUNTS_DATABASE`
or `$MOONGATE_REALM_DATABASE`. Merely exporting those variables does not override
a literal URI in the TOML file.

`MoongatePersistenceService` opens each active database and runs `SELECT 1`. The Redis service also checks its connection before accepting clients. Each PostgreSQL success
logs `Postgres connection successful` with the target and endpoint, without credentials.
A connection or ping failure throws and prevents other services from starting.
Moongate does not create missing databases; schema and migration checks run after
the connection checks. See [PostgreSQL persistence](persistence.md).

## Settings and validation

| Setting | Meaning and limits |
| --- | --- |
| `mode` | `login`, `game` or `standalone`; default standalone. Login runs account authentication, a login packet listener and realm directory; Game runs world services and publishes its realm to Redis; Standalone runs both roles and publishes its local realm to Redis. |
| `shard.shard_name` | Shard display metadata; used as the standalone list name when it fits the 32-character ASCII wire limit. Otherwise the local list name defaults to `Moongate`. |
| `network.login_port` | Login TCP listener port; default 2593. Used in login and standalone modes. |
| `network.game_port` | Game TCP listener port; default 2595. Used in game and standalone modes. Standalone rejects equal login and game ports. |
| `network.listen_address` | IP literal, not a DNS hostname. `0.0.0.0` makes the host enumerate local unicast addresses and create an endpoint for each active role on every address, including IPv6 addresses; it is not a single wildcard listener. Standalone therefore starts two listeners per address. Use a specific IP to restrict binding. |
| `network.enable_ping_server` | Serialized setting with no current runtime consumer. It does not disable the registered UO ping handler. |
| `ultima.ultima_path` | Existing, readable client data directory. Path and environment expansion apply; relative paths use the process working directory. It must contain `tiledata.mul`, the map and statics files of every map in `data/maps.toml`, and `MultiCollection.uop` or `multi.idx` with `multi.mul`; the server stops at startup when one is missing. |
| `persistence.auto_sync_schema` | Defaults to false. Normal startup checks versioned SQL history; when false it also fails if registered entities require DDL. Generate and review SQL, then apply it with the separate migration runner. Enable only as an explicit development convenience. |
| `persistence.accounts.connection_string` | Accounts/login PostgreSQL URI, or `$NAME` / `${NAME}` environment reference. Resolved only when registered entities use Accounts. |
| `persistence.realm.connection_string` | This realm's PostgreSQL URI, or `$NAME` / `${NAME}` environment reference. Resolved only when registered entities use Realm. |
| `redis.connection_string` | Shared Redis endpoint and password in StackExchange.Redis format, or a `$NAME` / `${NAME}` environment reference. Required by every runtime role. |
| `redis.handoff_secret` | Separate cluster-wide proof secret or environment reference. Required by every runtime role; at least 32 bytes after UTF-8 encoding. Never reuse the Redis password. |
| `realm_directory.realm_id` | Stable ID for a game realm and its Redis lease/ticket namespace. Standalone defaults to `local`; give each independently running realm a distinct ID. |
| `realm_directory.name`, `server_index` | ASCII list name (at most 32 characters) and unique index (0–65535). Standalone defaults to the shard name and index zero; set a distinct index for each realm sharing Redis. |
| `realm_directory.advertised_address`, `advertised_port` | Client-facing IPv4 literal and port. Required in game mode; standalone defaults to loopback and `network.game_port`. `0xA8` carries the address; `0x8C` carries the selected realm port. |
| `realm_directory.minimum_account_type` | Lowest account level allowed to see the realm; `regular`, `game_master` or `administrator`. |
| `realm_directory.heartbeat_interval_seconds`, `lease_duration_seconds`, `max_realms` | Defaults 5, 15 and 128. Lease duration must exceed two heartbeats; the Redis-backed directory caps realms at 128. |
| `world_save.enabled` | Starts periodic autosaving when true. Does not disable explicit saves or the eligible final shutdown save. |
| `world_save.interval_seconds` | Positive integer seconds, validated even when autosaving is disabled. |
| `diagnostics.enabled` | Starts the periodic diagnostic collector when true. |
| `diagnostics.interval_seconds` | Positive integer seconds; must fit the timer range (at most 4,294,967 seconds). |
| `diagnostics.log_metrics` | Logs periodic collected metrics when true. |
| `scripting.bootstrap_file` | Nonblank path resolved within the scripts root; must satisfy the script path restrictions. Missing file is a warning, execution failure aborts startup. |
| `scripting.max_instructions_per_resume` | Positive instruction budget for one coroutine resume. |
| `scripting.max_instructions_per_chunk` | Positive instruction budget for top-level chunk execution. |
| `scripting.hook_interval` | Positive instruction-check interval, no greater than either instruction budget. |
| `scripting.write_definitions` | Generates `definitions.lua` and `.luarc.json` for editor support. |
| `scripting.max_string_length` | Positive maximum result length enforced by `string.rep`, measured in UTF-16 characters; not a global Lua memory limit. |
| `localization.language` | Code of ASCII letters naming the texts file `data/messages/<language>.toml`; default `eng`. Shipped: `eng`, `ita`, `ger`, `fre`, `spa`, `por`, `pol`, `cze`. `eng.toml` must also exist: a message missing from the chosen language falls back to English. Used in game and standalone modes. See [Localization](localization.md). |
| `line_of_sight.max_distance` | From 1 to 255; default 25. The farthest a point can see along X or Y, as ModernUO; farther points are never in sight. Used in game and standalone modes. |

Redis is required at runtime in all three modes, including standalone. `redis.connection_string` is a StackExchange.Redis configuration string or an environment reference resolved at startup; the Docker example uses `redis:6379,password=...` on its private bridge. `redis.handoff_secret` is an independent cluster-wide secret, also supplied through an environment reference. Give the login and every game process the same values. The Docker example reads both from separate Compose secrets; keep the actual values out of TOML and the repository. A Redis connection failure prevents startup. A later Redis outage stops new realm lists and handoffs while existing game sessions continue; pending tickets are lost on Redis restart and game processes republish their leases. Configure Redis with `maxmemory-policy noeviction`.

Game-loop queue limits, timer-wheel resolution and packet dispatch limits use C# option objects rather than additional TOML sections. See [Game loop and timers](game-loop-and-timers.md), [Packets](packets.md) and the [Docker topology](docker-login-realms.md).

## Command line and root directory

Inspect the installed executable with `--help`. From a checkout:

```sh
dotnet run --project src/Moongate.Server -c Release -- --help
dotnet run --project src/Moongate.Server -c Release -- \
  --root-directory /absolute/path/to/moongate-data --pid-file-name moongate.pid
```

| Option | Default | Current behavior |
| --- | --- | --- |
| `--root-directory <path>` | Unset | Overrides `MOONGATE_ROOT`; with neither set the server refuses to start |
| `--pid-file-name <name>` | `moongate.pid` | PID filename under the chosen root; use a plain filename |
| `--log-level <level>` | `Information` | Parsed into server arguments, but currently not applied to the Serilog level policy |
| `--log-to-file` | `true` | File logging is enabled; the generated parser only accepts this as a presence flag |
| `--log-packets` | `false` | Sets the argument to true; currently no packet-tracing consumer |
| `--show-header` | `true` | Shows the startup banner; presence flag |
| `--persistence-schema <mode>` | `None` | `preview` prints draft PostgreSQL DDL; `generate` writes a draft file. The old `apply` mode directs you to `Moongate.MigrationRunner` |
| `--migration-target <target>` | Unset | Required by `generate`: `auth` or `world` |
| `--migration-output <path>` | Unset | Required by `generate`: new `NNNN_description.sql` file; refuses overwrite |
| `--initialize-root` | `false` | Prepare config/directories/bundled migrations offline, without starting the server |
| `--generate-admin-certificate` | `false` | With `--initialize-root`, create or reuse the administration TLS identity and enable `[admin_api]` |
| `--admin-certificate-hosts <names>` | Unset | Comma-separated DNS/IP SANs in addition to localhost; requires certificate generation |
| `--version` | — | Prints executable version |
| `-h`, `--help` | — | Prints usage |

Although help displays `<bool>` for the default-true options, the current CLI
does not accept `--show-header false`, `--show-header=false` or corresponding
file-logging forms. There is no CLI switch to turn these two options off yet.

Root precedence is **command line → `MOONGATE_ROOT`**. With neither set the root would
be the directory holding the binary, and the server refuses to start with exit code 2,
naming `--root-directory`. The same refusal applies when either resolves to that
directory, because an upgrade replaces it wholesale and would delete `config/`, `logs/`,
`save/` and `world-saves/` with it.
The chosen root expands home/environment references and becomes an absolute path;
a relative root starts from the working directory. Prefer explicit absolute paths
in service managers and containers. Docker sets `MOONGATE_ROOT=/data` by default.

The schema command loads plugin persistence registrations but does not acquire the
normal PID, start listeners/services, or generate runtime files. Stop the affected
runtime before applying reviewed DDL. See [First start](getting-started.md) for PID
ownership, logs and troubleshooting, [PostgreSQL persistence](persistence.md) for
connection, schema and world-save semantics, and
[Lua scripting](scripting.md) for budgets and sandbox boundaries.

Versioned SQL is applied by the isolated `migration-runner/Moongate.MigrationRunner`
executable using `status|apply --target auth|world`. In released artifacts its default
root is the parent server directory; `--root-directory` and `MOONGATE_ROOT` override
it. See [Generate, review and apply](persistence-migrations.md#generate-review-and-apply).

## Administration endpoint

`[admin_api]` configures the embedded gRPC plugin. It is disabled by default and uses server TLS on port 2590 when enabled. Certificate paths resolve relative to the root; password environment references are resolved only for enabled endpoints. Use [mgboot certificate setup](mgboot.md#generate-an-administration-certificate) to generate a passwordless PFX and enable the endpoint offline. See [Administration API](admin-api.md) for roles, permissions, first-admin provisioning and all limits.
