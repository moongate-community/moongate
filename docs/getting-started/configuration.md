# Configuration

Moongate reads a single YAML file, **`moongate.yaml`**, from the
[root directory](install-and-first-launch.md#the-root-directory). The first
launch generates it with defaults; edit and restart to apply changes.

## The `moongate` section

| Key | Type | Default | Meaning |
|---|---|---|---|
| `ShardName` | string | `Moongate` | The shard's name, shown in the client's server list. |
| `StatsRefreshSeconds` | int | `30` | How often the public statistics snapshot is recomputed on the game loop. |
| `UltimaDirectory` | string | — | Path to the UO client files. See the override note below. |
| `MapBlockCacheSize` | int | `4096` | How many 8×8-tile map blocks each facet keeps in memory, land and statics counted separately. See the note below. |
| `Network.Address` | string | `0.0.0.0` | Local bind address for the TCP listener. |
| `Network.Port` | int | `2593` | TCP port for both login and game traffic (single process, single port). |
| `Network.PublicAddress` | string | `127.0.0.1` | Address advertised to clients in the server list and game-server redirect. Must be reachable *from the client*. |

### Map block cache

Map blocks are read from the client files the first time something asks for them and kept in memory,
least-recently-used dropped past `MapBlockCacheSize`. The default of 4096 holds a contiguous
512×512-tile region per facet — far more than play needs, since a player sees roughly 18 tiles, and
enough that panning the web map viewer around one area does not re-read constantly.

Raise it on a shard whose players are spread thin across a facet; lower it on a memory-tight host.
`0` disables the cache and re-reads every block, which is correct but slow. The bound matters most
for the web map viewer: it streams tiles across a whole facet, and Felucca alone is 393,216 blocks.

> [!NOTE]
> **`UltimaDirectory` is currently always overridden at startup**: the
> `--uo-directory` command-line option defaults to `~/uo` and is applied on
> top of the config unconditionally, so the YAML value never wins. Treat
> `--uo-directory` (or its `~/uo` default) as the effective setting; the key
> remains in the file for when the CLI stops forcing a default.

## The `logger` section

Logging is provided by the SquidStd runtime (Serilog underneath) and
configured in the same file:

| Key | Default | Meaning |
|---|---|---|
| `MinimumLevel` | `Information` | Minimum log level (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`). |
| `EnableConsole` | `true` | Write log events to the console. |
| `EnableFile` | `false` | Also write rolling log files. |
| `LogDirectory` | `logs` | Directory (under the root) for log files. |
| `FileName` | `squidstd-.log` | Log file name pattern; the rolling date is appended. |
| `RollingInterval` | `Day` | How often a new log file starts. |

## Full generated example

This is verbatim what a first launch writes:

```yaml
logger:
  MinimumLevel: Information
  EnableConsole: true
  EnableFile: false
  LogDirectory: logs
  FileName: squidstd-.log
  RollingInterval: Day
moongate:
  ShardName: Moongate
  UltimaDirectory: /home/you/uo
  Network:
    Address: 0.0.0.0
    Port: 2593
    PublicAddress: 127.0.0.1
```

## Command-line options

The server binary itself takes a handful of options that interact with the
config:

| Option | Default | Meaning |
|---|---|---|
| `--root-directory <path>` | `MOONGATE_ROOT` env var, else `./moongate_root` | Where `moongate.yaml` and every runtime directory live. |
| `--uo-directory <path>` | `~/uo` | UO client files; overrides `UltimaDirectory` (see note above). |
| `--show-header <bool>` | `true` | Print the ASCII header at startup. |
