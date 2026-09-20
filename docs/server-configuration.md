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

[api]
enabled = false
listen_address = "0.0.0.0"
port = 2594
auto_generate_certificate = false
certificate_dns_names = ["localhost"]
certificate_ip_addresses = ["127.0.0.1", "::1"]
certificate_path = ""
certificate_password_environment_variable = "MOONGATE_API_CERTIFICATE_PASSWORD"
trusted_root_paths = []
peers = []

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
| `api.enabled` | Enables the internal MessagePack/mTLS listener; default false. Disabled APIs log a warning and leave handlers unfrozen; certificate I/O occurs only if generation is explicitly enabled. |
| `api.listen_address` | IPv4/IPv6 literal; default `0.0.0.0` binds one IPv4 wildcard listener. Unlike the game listener, it does not enumerate interfaces. |
| `api.port` | TCP port from 1 through 65535; default 2594. |
| `api.auto_generate_certificate` | Default false. Creates a missing PFX and exports its public `.pem` copy, even with `enabled = false`. Existing PFX files are never replaced. |
| `api.certificate_dns_names` | DNS SANs for generation; default `["localhost"]`. No URLs or wildcards. |
| `api.certificate_ip_addresses` | IP SANs for generation; default `["127.0.0.1", "::1"]`. No scope identifiers; at least one DNS name or IP is required across both arrays. |
| `api.certificate_path` | Local PKCS#12/PFX file containing the server leaf certificate and private key. |
| `api.certificate_password_environment_variable` | Name of the environment variable containing the PFX password. If named but unset, startup fails. An empty name permits an unencrypted PFX. Never put the password itself in TOML. |
| `api.trusted_root_paths` | When enabled, a nonempty array of trusted private CA certificates or explicitly trusted self-signed peer certificates (PEM or DER). Relative certificate/root paths resolve under `<root>/config`, independent of working directory. |
| `api.peers` | Nonempty array of allowed certificate identities; see the example below. Each fingerprint is unique ignoring case. |
| `api.peers.certificate_sha256` | Exactly 64 hexadecimal characters identifying the peer's leaf certificate; no colons. |
| `api.peers.peer_id` | Nonblank local identity for this peer. Multiple certificates may map to one identity during rotation. |
| `api.peers.allowed_operations` | `["*"]` grants all registered operations, including future additions. Otherwise use integer IDs from 1 through 65535. Empty or omitted denies all incoming operations; the wildcard must appear alone. |
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

Full API validation applies when `api.enabled` is true. Certificate provisioning
settings are also validated when `api.auto_generate_certificate` is true. Invalid API configuration,
missing/unreadable certificates, a local leaf outside its validity window, an explicit
EKU excluding server authentication, a missing private key, a wrong password or an
occupied port fail startup;
services already started are stopped in reverse order. With both options false, incomplete API
settings are ignored. Provisioning with the listener disabled does not require trust roots or peers. There is no plaintext fallback.

Game-loop queue limits, timer-wheel resolution and packet dispatch limits use C#
option objects rather than additional TOML sections. The hosted API uses the
library's default `ApiOptions` limits and timeouts. See
[Game loop and timers](game-loop-and-timers.md), [Packets](packets.md) and the
[internal API library](../src/Moongate.Api/README.md).

## Enable the internal API server

This integration is available in builds containing the API hosting change. Older
release images require upgrading or building the current checkout.

1. Provision certificates using the [API certificate guide](api-certificates.md).
   It covers automatic self-signed generation with the port closed, public
   certificate exchange, passwords, Docker and renewal. The example below uses
   an externally issued PFX and private CA root.
   The server needs `serverAuth` usage and a DNS name matching the client's TLS
   target host; clients need `clientAuth`. Mount certificates read-only where
   possible and allow the runtime user to read them.
2. Replace the generated `[api]` section with this example. Substitute the
   client leaf's SHA-256 fingerprint for the illustrative value:

   ```toml
   [api]
   enabled = true
   listen_address = "0.0.0.0"
   port = 2594
   certificate_path = "tls/server.pfx"
   certificate_password_environment_variable = "MOONGATE_API_CERTIFICATE_PASSWORD"
   trusted_root_paths = ["tls/root.pem"]

   [[api.peers]]
   certificate_sha256 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"
   peer_id = "admin-console"
   allowed_operations = ["*"]
   ```

   Use `["*"]` for a fully trusted peer, or an explicit list such as `[100, 200]`
   to restrict its operations. `[]` and omission keep all operations denied.

3. Inject `MOONGATE_API_CERTIFICATE_PASSWORD` from your credential provider into
   the server environment and restart. A successful bind logs `API listener
   started at ...` with the actual endpoint and contract/handler counts. The
   default disabled state logs `API server is disabled` with activation guidance.

The host registers `IApiServerService` as a singleton. It starts at priority 110,
after game packet services, and drains/disposes the listener before they stop.
`Endpoint` is the actual bound endpoint while accepting connections, otherwise
null. A stopped host service is terminal: start a new host to reload configuration
or certificate permissions.

Register typed handlers in `Program.cs`'s `RegisterServices` callback or a plugin's
`Register(Container)` method, before startup:

```csharp
// using Moongate.Server.Extensions;
// IncrementHandler implements IApiHandler<IncrementRequest, IncrementResponse>.
container.RegisterApiHandler<IncrementHandler>();
```

The [complete typed handler example](../src/Moongate.Api/README.md#handle-requests-and-open-a-channel)
shows these request/response types. Plugin registration runs before the API registry
freezes at startup; the same registry is used regardless of registration order.
Handler service dependencies that require startup must start before priority 110.
API handlers execute outside the game loop; explicitly marshal world changes to
`IGameLoopService` as described in [Game loop and timers](game-loop-and-timers.md).

The listener speaks **MessagePack over mutual TLS/TCP**, not HTTP. No built-in
login, realm discovery or administration operations are registered yet. A listener
with zero handlers can authenticate configured peers but cannot serve application
requests. See [Docker](docker.md#internal-api-port) for private-network deployment.

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
[world saves](persistence.md) for backup semantics and
[Lua scripting](scripting.md) for budgets and sandbox boundaries.
