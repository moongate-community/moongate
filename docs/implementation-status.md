# Implementation status

Moongate is under active development. This page is the one place that says what
the server does today and what it does not, so the other guides can describe a
mechanism without repeating the caveat. It describes the current source tree;
the [changelog](../CHANGELOG.md) records what each published release added.

**In one sentence:** the transport, packet pipeline, scripting, persistence and
Redis-backed realm discovery and login-to-game handoff are in place; account
authentication reaches a game session, while character selection and a playable world are not.

| Area | Works today | Not built yet |
| --- | --- | --- |
| Transport | Framed TCP listener and client, per-connection pipelines, connection and session registries, graceful shutdown | |
| Packets | Wire table, typed packet definitions and game-loop/async handlers; `0x80` login, `0xA8` list, `0xA0` selection, `0x8C` redirect, raw game seed and `0x91` handoff | Character list, movement and world packets |
| Login and realms | `mode` selects separate login/game services or combined standalone. Redis leases advertise live realms; login filters by account level and issues one-use handoff tickets | Character selection and world entry |
| Game loop | Single owner thread, bounded queues, timer wheel, admission and completion semantics | |
| Scripting | Sandboxed Lua 5.2, deterministic instruction budget, `engine`, `log`, `timer` modules, `wait`, reload, editor definitions; C# modules from plugins | World, character and inventory APIs |
| Persistence | Entity registration on two databases, async reads and writes, transactions, automatic Serial assignment, world saves, versioned SQL with a separate runner, development migration generation | Core world catalog has no tables; no database backup or restore |
| Accounts | `AccountEntity` in Accounts; `IAccountService` creates, lists and verifies login; console `account create`; game accepts a valid handoff without Accounts database access | No in-game command input yet |
| Shared transient state | Private Redis with expiring, fenced realm leases and one-use handoff tickets; startup checks connectivity and rejects new handoffs during outages | General cache use and other cross-process features |
| Templates | `ItemTemplate` and `LootTemplate` shapes, `EnumValueSpec`, `RangeValueSpec`, TOML converters, loader contract | A loader that reads `templates/`; nothing under it is loaded |
| Localization | `localization.language`, message files in 8 languages ported from UOX3 with English fallback, startup validation, `ILocalizationService`; [guide](localization.md) | Per-player language; no code sends these messages yet |
| Administration | Embedded optional gRPC plugin, private server TLS, Redis sessions, account login/list/create/revoke and server info; [guide](admin-api.md) | Panel backend/UI and character operations |
| Plugins | Assemblies under `plugins/` registering services, commands, Lua modules, metric providers, entities and SQL | |
| Diagnostics | Periodic process metrics, plugin metric providers, snapshot events | |
| Tools | Migration runner (0.6.0); `mgboot` root preparation with optional [administration TLS certificate setup](mgboot.md#generate-an-administration-certificate), and `mg-uoxconv` after 0.6.0 | |
| Docker | Source-built [one login and two game instances example](docker-login-realms.md) with role-local PostgreSQL credentials, private Redis and a lease smoke test | Character selection and world entry |

Settings that exist only as a contract: `network.enable_ping_server`, and
the `--log-level` and `--log-packets` command-line options are parsed and validated
but have no runtime consumer; see the
[configuration reference](server-configuration.md#settings-and-validation).
