# Implementation status

Moongate is under active development. This page is the one place that says what
the server does today and what it does not, so the other guides can describe a
mechanism without repeating the caveat. It describes the current source tree;
the [changelog](../CHANGELOG.md) records what each published release added.

**In one sentence:** the transport, packet pipeline, scripting, persistence, shard data,
client-file readers with movement and line-of-sight checks, and Redis-backed realm
discovery and login-to-game handoff are in place; account
authentication reaches a game session, which receives an empty character list; character
creation, selection and a playable world are not.

| Area | Works today | Not built yet |
| --- | --- | --- |
| Transport | Framed TCP listener and client, per-connection pipelines, connection and session registries, graceful shutdown | |
| Packets | Wire table, typed packet definitions and game-loop/async handlers; plugins add incoming packets with `RegisterIncomingPacket`; `0x80` login, `0xA8` list, `0xA0` selection, `0x8C` redirect, raw game seed, `0x91` handoff, then Huffman-compressed `0xB9` features and `0xA9` character list (empty slots, starting cities); `0xF8`, `0x8D` and `0xD9` are decoded | Character creation and selection handlers, movement and world packets |
| Login and realms | `mode` selects separate login/game services or combined standalone. Redis leases advertise live realms; login filters by account level and issues one-use handoff tickets | Character creation, selection and world entry |
| Game loop | Single owner thread, bounded queues, timer wheel, admission and completion semantics | |
| Scripting | Sandboxed Lua 5.2, deterministic instruction budget, `engine`, `log`, `timer` modules, `wait`, reload, editor definitions; C# modules from plugins | World, character and inventory APIs |
| Persistence | Entity registration on two databases, async reads and writes, transactions, automatic Serial assignment, world saves, versioned SQL with a separate runner, development migration generation | Core world catalog has no tables yet: `MobileEntity` (`world.mobiles`) is defined but not registered and has no migration; no database backup or restore |
| Accounts | `AccountEntity` in Accounts; `IAccountService` creates, lists and verifies login; console `account create`; game accepts a valid handoff without Accounts database access | No in-game command input yet |
| Shared transient state | Private Redis with expiring, fenced realm leases and one-use handoff tickets; startup checks connectivity and rejects new handoffs during outages | General cache use and other cross-process features |
| Shard data | Maps, starting cities, skills, professions, races, banned names, containers, bodies, weather and regions loaded from `data/` with startup validation; shipped with the server and copied by `mgboot`; [guide](data-files.md) | Only maps (`IMapService`), starting cities (character list) and messages (localization) are used at runtime; the rest is loaded and validated only |
| Client files | `ultima.ultima_path` checked at startup, client version logged, `tiledata.mul` loaded into `TileData` (land and item flags, heights, weights, names) and read through `ITileDataService`; map terrain and statics of every `maps.toml` map (MUL or UOP) read through `IMapService` with a block cache; every multi layout (houses, boats) from `MultiCollection.uop`, or `multi.idx` with `multi.mul`, read through `IMultiService`; one step's walkability and landing Z through `IMovementService` (terrain and statics, as ModernUO); line of sight through `ILineOfSightService` (POL's integer line walk, ModernUO's rules, `line_of_sight.max_distance`); [guide](world-queries.md) | Movement packets, items and mobiles in the movement check, items, mobiles and multis in line of sight, house placement |
| Templates | `ItemTemplate` and `LootTemplate` shapes, `EnumValueSpec`, `RangeValueSpec`, TOML converters, loader contract | A loader that reads `templates/`; nothing under it is loaded |
| Localization | `localization.language`, message files in 8 languages ported from UOX3 with English fallback, startup validation, `ILocalizationService` and the `localization` Lua module; [guide](localization.md) | Per-player language; no code sends these messages yet |
| Administration | Embedded optional gRPC plugin, private server TLS, Redis sessions, account login/list/create/revoke and server info; [guide](admin-api.md) | Panel backend/UI and character operations |
| Plugins | Assemblies under `plugins/` registering services, commands, Lua modules, metric providers, entities and SQL | |
| Diagnostics | Periodic process metrics, plugin metric providers, snapshot events | |
| Tools | Migration runner (0.6.0); `mgboot` root preparation with optional [administration TLS certificate setup](mgboot.md#generate-an-administration-certificate), and `mg-uoxconv` after 0.6.0 | |
| Docker | Source-built [one login and two game instances example](docker-login-realms.md) with role-local PostgreSQL credentials, private Redis and a lease smoke test | Character selection and world entry |

Settings that exist only as a contract: `network.enable_ping_server`, and
the `--log-level` and `--log-packets` command-line options are parsed and validated
but have no runtime consumer; see the
[configuration reference](server-configuration.md#settings-and-validation).
