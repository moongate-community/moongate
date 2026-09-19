<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="220" />
</p>

<h1 align="center">Moongate</h1>

<p align="center">
  <a href="https://github.com/moongate-community/moongate/actions/workflows/ci.yml"><img src="https://github.com/moongate-community/moongate/actions/workflows/ci.yml/badge.svg?branch=develop" alt="CI"></a>
  <a href="https://github.com/moongate-community/moongate/actions/workflows/security.yml"><img src="https://github.com/moongate-community/moongate/actions/workflows/security.yml/badge.svg?branch=main" alt="Security Audit"></a>
  <a href="https://github.com/moongate-community/moongate/pkgs/container/moongate"><img src="https://img.shields.io/badge/ghcr.io-moongate-2496ED?logo=docker&logoColor=white" alt="Container image"></a>
  <img src="https://img.shields.io/badge/platform-.NET%2010-blueviolet" alt=".NET 10">
  <img src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue" alt="AGPL-3.0-or-later">
</p>

## Docker

Every release publishes a `linux/amd64` image to the GitHub Container Registry,
tagged with the version and with `latest`.

```bash
docker pull ghcr.io/moongate-community/moongate:latest
```

The server listens on port 2593 and keeps its configuration, logs, plugins and
saves under `/data`. Mount a volume there so that state outlives the container.

```bash
docker run -d --name moongate \
  -v moongate-data:/data \
  -p 2593:2593 \
  ghcr.io/moongate-community/moongate:latest
```

The first start writes `/data/config/moongate.toml` and then exits, because
`ultima_path` still holds its placeholder. Mount your Ultima Online client files
and point that setting at them:

```toml
[ultima]
ultima_path = "/uo"
```

```bash
docker run -d --name moongate \
  -v moongate-data:/data \
  -v /path/to/ultima:/uo:ro \
  -p 2593:2593 \
  ghcr.io/moongate-community/moongate:latest
```

`MOONGATE_ROOT` and `--root-directory` move that root elsewhere. Run several
shards from the same image by giving each container its own volume and its own
published port; a root is meant for one server at a time.

[Diagnostics](docs/diagnostics.md)

[Dependency security audit](docs/security-audit.md)

## Server mode

Set `mode` at the root of `config/moongate.toml`, before any table headers:

```toml
mode = "standalone"
```

Supported values are `"login"`, `"game"`, and `"standalone"`. Omitting the setting
defaults to standalone. In C#, `MoongateServerConfig.Mode` uses the `ServerMode`
flags enum, where `Standalone = Login | Game`; an empty or unknown mode is rejected.

This setting currently defines the configuration contract. It does not yet select
which services start; separate login and game runtimes will use it in a subsequent change.

## Scripting

Shard content runs in an embedded Lua 5.2 runtime (`Moongate.Scripting`, on
[LuaCSharp](https://github.com/nuskey8/Lua-CSharp)) that lives entirely on the game
loop thread. Scripts sit under `scripts/` in the server root; `init.lua` runs at
startup, and `require` resolves only inside that directory, symbolic links included.

```lua
-- scripts/init.lua
log.info("booted {Engine} {Version}", engine.name, engine.version)

timer.every(30, function()
    log.info("tick")
    wait(2)                 -- parks this coroutine on the timer wheel
    log.info("two seconds later")
end)
```

- **Modules:** `engine` (name, version, codename, platform), `log` (`debug`, `info`, `warning`,
  `error`, Serilog templates), `timer` (`after`, `every`, `cancel`) and the global
  `wait(seconds)`; `print` goes to the server log. Host modules are C# classes marked
  `[ScriptModule]` / `[ScriptFunction]` / `[ScriptConstant]`, registered with
  `RegisterScriptModule<T>()` in `Program.cs`.
- **Budget:** a deterministic instruction count, not a wall clock. A coroutine resume
  may run 150,000 instructions and a top-level chunk 10,000,000 before it is aborted
  with a script error; `string.rep` refuses results longer than 16,777,216 characters. Nothing a script does
  can block or fault the loop.
- **Sandbox:** base, `string`, `table`, `math`, `coroutine` and `package` only; no `io`,
  `os`, `debug`, `dofile`, `loadfile`, `rawset` or script-created coroutines.
- **Errors:** every failure is logged with file and line and published as
  `ScriptErrorEvent` on the event bus; only an `init.lua` failure refuses the start.
- **Tooling:** at startup the engine writes `scripts/definitions.lua` and
  `scripts/.luarc.json`, so an editor with the Lua language server completes every
  bound module, function, constant and enum. The console offers `script reload <file>`
  and `script metrics`.

```toml
[scripting]
bootstrap_file = "init.lua"
max_instructions_per_resume = 150000
max_instructions_per_chunk = 10000000
hook_interval = 1000
write_definitions = true
max_string_length = 16777216
```

The package README, [src/Moongate.Scripting/README.md](src/Moongate.Scripting/README.md),
documents the binding model and the sandbox in full.

## Libraries

The eight library packages have their own English READMEs and runnable examples.
See [NuGet libraries and package verification](docs/nuget-packaging.md) for the
package list, dependencies, and the local verification command.
