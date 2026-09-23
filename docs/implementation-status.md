# Implementation status

Moongate is under active development. This page is the one place that says what
the server does today and what it does not, so the other guides can describe a
mechanism without repeating the caveat. It describes the current source tree;
the [changelog](../CHANGELOG.md) records what each published release added.

**In one sentence:** the transport, packet pipeline, scripting, persistence and
internal API infrastructure are in place; successful account login can return a
realm list, while realm selection and a playable world are not.

| Area | Works today | Not built yet |
| --- | --- | --- |
| Transport | Framed TCP listener and client, per-connection pipelines, connection and session registries, graceful shutdown | |
| Packets | Wire table, typed packet definitions, game-loop and async login handlers; successful AccountLogin returns `0xA8` and failures return `0x82` | `0xA0` selection, `0x8C` redirect, character list, movement and world packets |
| Login and realms | `mode` selects separate login/game services or combined standalone. Game realms register and renew over mTLS; login filters the live list by account level | Client realm selection and secure handoff tickets; game-side account verification API |
| Game loop | Single owner thread, bounded queues, timer wheel, admission and completion semantics | |
| Scripting | Sandboxed Lua 5.2, deterministic instruction budget, `engine`, `log`, `timer` modules, `wait`, reload, editor definitions; C# modules from plugins | World, character and inventory APIs |
| Persistence | Entity registration on two databases, async reads and writes, transactions, automatic Serial assignment, world saves, versioned SQL with a separate runner, development migration generation | Core world catalog has no tables; no database backup or restore |
| Accounts | `AccountEntity` in the Accounts database; `IAccountService` creates, lists and verifies a login (password hash and lock state); console `account create` command in login/standalone | Realm selection and game-side account verification; no in-game command input yet |
| Internal API | MessagePack over mutual TLS, typed request/reply handlers, channels, certificate generation, built-in realm register/renew/unregister on login | Additional shared account and game operations |
| Templates | `ItemTemplate` and `LootTemplate` shapes, `EnumValueSpec`, `RangeValueSpec`, TOML converters, loader contract | A loader that reads `templates/`; nothing under it is loaded |
| Plugins | Assemblies under `plugins/` registering services, commands, Lua modules, metric providers, entities and SQL | |
| Diagnostics | Periodic process metrics, plugin metric providers, snapshot events | |
| Tools | Migration runner (0.6.0); `mgboot` and `mg-uoxconv` after 0.6.0 | |
| Docker | Release image; the [one login and two game instances example](docker-login-realms.md) with role-local database credentials and mTLS realm discovery | Client selection and transfer from login to a game realm |

Settings that exist only as a contract: `network.enable_ping_server`, and
the `--log-level` and `--log-packets` command-line options are parsed and validated
but have no runtime consumer; see the
[configuration reference](server-configuration.md#settings-and-validation).
