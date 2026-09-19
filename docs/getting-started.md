# Start a Moongate server

This guide runs the server from source. For the released container, use
[Run with Docker](docker.md). Moongate is under active development: the transport,
packet pipeline, scripting and persistence infrastructure are available, but a
complete account login and playable world are not implemented yet.

## Requirements

Install the .NET 10 SDK selected by `global.json` and Git. Supply your own Ultima
Online client data; Moongate does not distribute it. The initial packet protocol
targets ClassicUO 7.x. Use a writable server root and a free TCP port (2593 by
default). Node.js is only needed to work on the documentation website.

## Build and configure

1. Clone and build the source:

   ```sh
   git clone https://github.com/moongate-community/moongate.git
   cd moongate
   git switch develop
   dotnet build Moongate.slnx -c Release
   ```

   `develop` includes unreleased work. To reproduce a release, check out its tag
   instead and use the documentation published for that version.

2. Start once with a dedicated data directory:

   ```sh
   dotnet run --project src/Moongate.Server -c Release --no-build -- \
     --root-directory /absolute/path/to/moongate-data
   ```

   On a fresh root, the server creates `config/moongate.toml`. It then exits
   because the default client path is `ChangeMe`. This is the expected first-run
   configuration step; do not create an empty directory just to bypass it.

3. Edit the generated file, preserving its other sections:

   ```toml
   [ultima]
   ultima_path = "/absolute/path/to/your/ultima-client"
   ```

   Use an absolute path. Relative client paths resolve from the process working
   directory, not from the server root. See the complete
   [configuration reference](server-configuration.md) for listener and save settings.

4. Run the same command again. Check the startup logs for loaded services and
   bound endpoints. A missing `scripts/init.lua` produces a warning and starts
   an empty scripting environment. A bootstrap script that exists but fails
   prevents startup. Add scripts using [Writing Lua scripts](scripting.md).

5. Stop with Ctrl+C and allow shutdown to finish. After successful startup the
   host coordinates the final world save before closing persistence. A failed
   startup or faulted game loop cannot promise a final save. Do not terminate
   the process while waiting for a save or backup.

## Files and process ownership

All server-managed paths below are relative to `--root-directory`:

| Path | Purpose |
| --- | --- |
| `config/moongate.toml` | Server configuration; created once with defaults |
| `logs/moongate-*.clef` | Structured JSON log events, one per line |
| `plugins/` | One assembly bundle per plugin directory |
| `scripts/` | Lua source and generated editor definitions |
| `save/` | Live persistence snapshots and journals |
| `world-saves/` | Consistent world-save backup generations |
| `moongate.pid` | Current process identifier |
| `moongate.pid.lock` | Lock file used to exclude another instance |

The PID guard is acquired before configuration is loaded. A live PID or an
already-held lock rejects another start. A stale or malformed PID is replaced.
The guard checks process liveness, not executable identity, so a reused PID can
also reject startup. Investigate that process before changing the PID file.
Normal cleanup removes the PID file if it still belongs to this process; the
`.lock` file may remain after its handle is released. Its presence alone does
not mean the server is running.

Use a different root and listener port for each instance. Changing only the PID
filename does not make a shared save directory safe for multiple writers.

Console logs show time, level, source and message. File logs roll daily and at
10 MiB, keeping up to 30 files. For metrics see [Diagnostics](diagnostics.md);
for backup and recovery see [Persistence and world saves](persistence.md).

## Common startup problems

| Symptom | Check |
| --- | --- |
| Client path error | Set `ultima.ultima_path` to readable, real client data |
| TOML parse or validation error | Fix the named field; existing files are not silently replaced |
| Port binding failure | Check `network.listen_address`, port availability and interface addresses |
| Another instance detected | Check the PID and running process; use a separate root for another server |
| Script startup error | Fix `scripts/init.lua`; inspect the script filename and line in the log |

The [transport ownership guide](network-game-separation.md) explains the current
login/game separation boundary. Setting `mode = "login"` alone does not create
a login-only deployment.
