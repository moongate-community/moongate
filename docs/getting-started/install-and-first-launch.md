# Install & first launch

## Prerequisites

- **.NET SDK 10.0** (the repo pins the exact version in `global.json`;
  `10.0.301` or a later patch of it).
- **UO client files** — a ClassicUO-compatible client installation on the
  same machine. The server reads art, map and data files from that directory
  at startup and refuses to start without it.

## Build and run

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet run --project src/Moongate.Server -- --uo-directory ~/uo
```

`--uo-directory` must point at your UO client files (default: `~/uo`).

On the first launch you will see the Moongate ASCII header (suppress it with
`--show-header false`), and the log will warn loudly:

```text
!!!! Default account created!!!!
!!!! Username: admin, Password: admin !!!!
```

That seeded **admin / admin** account (administrator level) is what you log
in with — change it before exposing the server to anyone else.

## The root directory

Moongate keeps everything it writes in one **root directory**, resolved in
this order:

1. `--root-directory <path>` command-line option,
2. `MOONGATE_ROOT` environment variable,
3. `./moongate_root` under the current working directory (default).

The first run creates and seeds it:

| Entry | Contents |
|---|---|
| `moongate.yaml` | The configuration file, generated with defaults — see [Configuration](configuration.md). |
| `data/` | World data (containers, signs, teleporters, regions, names, professions, …), seeded from the embedded defaults on first run. Edit these to customize the world. |
| `templates/` | [Item](../scripting/data/item-templates.md), [mobile](../scripting/data/mobile-templates.md) and [loot](../scripting/data/loot-tables.md) YAML templates, also seeded from the embedded defaults. |
| `scripts/` | Your [Lua scripts](../scripting/guides/how-scripting-works.md) (`bootstrap.lua`, `init.lua`, `main.lua`). |
| `plugins/` | Drop-in plugin assemblies, loaded at startup. |
| `saves/` | Binary persistence snapshots (accounts, mobiles, items). The seeded admin account lands in the first snapshot. |

## Verify it is alive

A healthy first launch logs the plugin chain, the data loaders (item,
mobile and loot templates included), and ends with the network service
listening on `0.0.0.0:2593`. At that point you are ready to
[connect a client](connect-with-classicuo.md).
