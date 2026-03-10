# Configuration Guide

This guide reflects the current runtime behavior in Moongate v2.

## Configuration Sources

Priority order:

1. Command-line arguments
2. Environment variables
3. `moongate.json` in root directory
4. Defaults

## Command-Line Arguments

Current `Program` command signature supports:

- `--show-header` (`bool`, default `true`)
- `--root-directory` (`string`)
- `--uo-directory` (`string`)
- `--loglevel` (`LogLevelType`, default `Debug`)

Example:

```bash
dotnet run --project src/Moongate.Server -- \
  --root-directory /opt/moongate \
  --uo-directory /opt/uo \
  --loglevel Information
```

## Environment Variables

Configuration env support is generic and maps to the full `MoongateConfig` model:

- `MOONGATE_ROOT_DIRECTORY`
- `MOONGATE_UO_DIRECTORY`
- `MOONGATE_<PROPERTY>`
- `MOONGATE_<SECTION>__<PROPERTY>`
- `MOONGATE_<SECTION>__<SUBSECTION>__<PROPERTY>`

Examples:

- `MOONGATE_HTTP__PORT=8088`
- `MOONGATE_HTTP__JWT__SIGNING_KEY=change-me`
- `MOONGATE_SPATIAL__SECTOR_ENTER_SYNC_RADIUS=3`

Additional runtime env vars (outside `MoongateConfig`):

- `MOONGATE_ADMIN_USERNAME`
- `MOONGATE_ADMIN_PASSWORD`
- `MOONGATE_UI_DIST`

## `moongate.json`

Location:

- `<RootDirectory>/moongate.json`

If missing, bootstrap creates one with default values.

### Current Merge Behavior

At startup, bootstrap loads configuration with standard provider precedence:

1. `moongate.json`
2. `MOONGATE_*` environment variables (override file values)

## Config Model

Top-level shape:

```json
{
  "rootDirectory": "/opt/moongate",
  "uoDirectory": "/opt/uo",
  "logLevel": "Information",
  "logPacketData": true,
  "isDeveloperMode": false,
  "http": {
    "isEnabled": true,
    "port": 8088,
    "websiteUrl": "http://localhost",
    "isOpenApiEnabled": true
  },
  "game": {
    "shardName": "Moongate Shard",
    "timerTickMilliseconds": 250,
    "timerWheelSize": 512,
    "idleCpuEnabled": true,
    "idleSleepMilliseconds": 1
  },
  "metrics": {
    "enabled": true,
    "intervalMilliseconds": 1000,
    "logEnabled": true,
    "logToConsole": false,
    "logLevel": "Trace"
  },
  "scripting": {
    "enableFileWatcher": true
  },
  "email": {
    "isEnabled": false,
    "fromAddress": "noreply@localhost",
    "fallbackLocale": "en",
    "smtp": {
      "host": "localhost",
      "port": 25,
      "useSsl": false,
      "username": null,
      "password": null
    }
  },
  "persistence": {
    "saveIntervalSeconds": 30
  }
}
```

## Directories

`DirectoriesConfig` auto-creates directory tree under root using `DirectoryType` values:

- `data`
- `templates`
- `scripts`
- `save`
- `logs`
- `cache`
- `email/templates`

## Scripting

Current scripting runtime option:

- `Scripting.EnableFileWatcher` (`bool`, default `true`)
  - `true`: enables `FileSystemWatcher` on `scripts/**/*.lua` for live reload notifications
  - `false`: disables watcher creation entirely
- `Scripting.LuaBrainMaxBrainsPerTick` (`int`, default `0`)
  - `<= 0`: no explicit per-tick cap
  - `> 0`: limits how many due NPC brains are processed per tick (helps smooth spikes under heavy load)

## Email Templates

Email templates are resolved from:

- `DirectoriesConfig[DirectoryType.EmailTemplates]`

Default bundled templates:

- `registration_ok` (`en.subject.sbn`, `en.text.sbn`, `en.html.sbn`)
- `recover_password` (`en.subject.sbn`, `en.text.sbn`, `en.html.sbn`)

Scriban templates receive a global `websiteUrl` value from `Http.WebsiteUrl`.

When `Email.IsEnabled = false`, the runtime uses a no-op sender and does not perform SMTP delivery.

## HTTP Endpoints

When HTTP is enabled:

- `/` → UI frontend when UI hosting is enabled (default)
- `/health` → plain text `ok`
- `/metrics` → Prometheus text format (if metrics factory configured)
- `/scalar` and `/openapi/*` (if OpenAPI enabled)

## Persistence Setting

Only persistence knob currently exposed:

- `Persistence.SaveIntervalSeconds` (autosave interval)

## Docker Notes

For container runs, typical environment:

```bash
MOONGATE_ROOT_DIRECTORY=/app
MOONGATE_UO_DIRECTORY=/uo
```

Mount `/app` for runtime data and `/uo` for UO client files.

For a full variable list and a complete `docker-compose` sample, see `README.md` -> **Environment Configuration**.

---

**Previous**: [Installation Guide](installation.md) | **Next**: [Quick Start](quickstart.md)
