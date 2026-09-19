# Server configuration

The host loads `<root>/config/moongate.toml` with `ConfigHelper.Load`. A missing
file and its parent directory are created with defaults. Existing files are
deserialized and validated without being rewritten; omitted properties keep
their model defaults. Invalid TOML, invalid settings and filesystem errors fail
startup. Changes take effect at the next start; there is no configuration reload.

## Complete default configuration

TOML keys use `snake_case`. Keep `mode` before the first table header:

```toml
# Configuration contract only: currently does not select service composition.
mode = "standalone"

[shard]
shard_name = "Moongate"

[network]
game_port = 2593
listen_address = "0.0.0.0"
enable_ping_server = true # Reserved: currently not consumed by the host.

[ultima]
ultima_path = "ChangeMe" # Replace with your client data directory.

[world_save]
enabled = true # Enables periodic saves; manual/final saves remain available.
interval_seconds = 300
backups_enabled = true
backup_retention_count = 5

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
```

## Settings and validation

| Setting | Meaning and limits |
| --- | --- |
| `mode` | `login`, `game` or `standalone`; default standalone. Empty and unknown values fail. Maps to `ServerMode`, with `Standalone = Login \| Game`. Service selection is not implemented yet. |
| `shard.shard_name` | Shard display metadata; does not implement realm discovery or a server list by itself. |
| `network.game_port` | TCP listener port; use a distinct port for each local instance. |
| `network.listen_address` | IP literal, not a DNS hostname. `0.0.0.0` makes the host enumerate local unicast addresses and create endpoints for them, including IPv6 addresses; it is not a single wildcard listener. Use a specific IP to restrict binding. |
| `network.enable_ping_server` | Serialized setting with no current runtime consumer. It does not disable the registered UO ping handler. |
| `ultima.ultima_path` | Existing, readable client data directory. Path and environment expansion apply; relative paths use the process working directory. |
| `world_save.enabled` | Starts periodic autosaving when true. Does not disable explicit saves or the eligible final shutdown save. |
| `world_save.interval_seconds` | Positive integer seconds, validated even when autosaving is disabled. |
| `world_save.backups_enabled` | Writes a consistent backup generation after a save when true. |
| `world_save.backup_retention_count` | Positive integer, even when backups are disabled; maximum completed managed generations retained. |
| `diagnostics.enabled` | Starts the periodic diagnostic collector when true. |
| `diagnostics.interval_seconds` | Positive integer seconds; must fit the timer range (at most 4,294,967 seconds). |
| `diagnostics.log_metrics` | Logs periodic collected metrics when true. |
| `scripting.bootstrap_file` | Nonblank path resolved within the scripts root; must satisfy the script path restrictions. Missing file is a warning, execution failure aborts startup. |
| `scripting.max_instructions_per_resume` | Positive instruction budget for one coroutine resume. |
| `scripting.max_instructions_per_chunk` | Positive instruction budget for top-level chunk execution. |
| `scripting.hook_interval` | Positive instruction-check interval, no greater than either instruction budget. |
| `scripting.write_definitions` | Generates `definitions.lua` and `.luarc.json` for editor support. |
| `scripting.max_string_length` | Positive maximum result length enforced by `string.rep`, measured in UTF-16 characters; not a global Lua memory limit. |

Game-loop queue limits, timer-wheel resolution, packet dispatch limits and
internal API options are configured through their C# option objects in the host;
they are not additional sections of this TOML file. See
Game loop and timers, Packets and the
[internal API library](../src/Moongate.Api/README.md).

## Command line and root directory

Inspect the installed executable with `--help`. From a checkout:

```sh
dotnet run --project src/Moongate.Server -c Release -- --help
dotnet run --project src/Moongate.Server -c Release -- \
  --root-directory /absolute/path/to/moongate-data --pid-file-name moongate.pid
```

| Option | Default | Current behavior |
| --- | --- | --- |
| `--root-directory <path>` | Unset | Overrides `MOONGATE_ROOT`; otherwise the executable directory is used |
| `--pid-file-name <name>` | `moongate.pid` | PID filename under the chosen root; use a plain filename |
| `--log-level <level>` | `Information` | Parsed into server arguments, but currently not applied to the Serilog level policy |
| `--log-to-file` | `true` | File logging is enabled; the generated parser only accepts this as a presence flag |
| `--log-packets` | `false` | Sets the argument to true; currently no packet-tracing consumer |
| `--show-header` | `true` | Shows the startup banner; presence flag |
| `--version` | — | Prints executable version |
| `-h`, `--help` | — | Prints usage |

Although help displays `<bool>` for the default-true options, the current CLI
does not accept `--show-header false`, `--show-header=false` or corresponding
file-logging forms. There is no CLI switch to turn these two options off yet.

Root precedence is **command line → `MOONGATE_ROOT` → executable directory**.
The chosen root expands home/environment references and becomes an absolute path;
a relative root starts from the working directory. Prefer explicit absolute paths
in service managers and containers. Docker sets `MOONGATE_ROOT=/data` by default.

See [First start](getting-started.md) for PID ownership, logs and troubleshooting,
world saves for backup semantics and
Lua scripting for budgets and sandbox boundaries.
