# Implementation status

Moongate is under active development. This page is the one place that says what
the server does today and what it does not, so the other guides can describe a
mechanism without repeating the caveat. It reflects 0.6.0 and the `develop` branch
at the time of publication; the [changelog](../CHANGELOG.md) records what each
release added.

**In one sentence:** the transport, packet pipeline, scripting, persistence and
internal API infrastructure are in place; account login, realm selection and a
playable world are not.

| Area | Works today | Not built yet |
| --- | --- | --- |
| Transport | Framed TCP listener and client, per-connection pipelines, connection and session registries, graceful shutdown | |
| Packets | Wire table, typed packet definitions, synchronous handlers on the game loop and bounded async handler support; host handlers for Ping, ClientVersion, LoginSeed, and async AccountLogin credential checks | Realm list, character list, movement and world packets; a successful account check does not complete login |
| Login and realms | The `mode` setting is validated (`login`, `game`, `standalone`); AccountLogin can verify credentials and set the session account | `mode` selects nothing: every process runs the same services. No completed login handshake, shared account API, realm registration, discovery or handoff |
| Game loop | Single owner thread, bounded queues, timer wheel, admission and completion semantics | |
| Scripting | Sandboxed Lua 5.2, deterministic instruction budget, `engine`, `log`, `timer` modules, `wait`, reload, editor definitions; C# modules from plugins | World, character and inventory APIs |
| Persistence | Entity registration on two databases, async reads and writes, transactions, automatic Serial assignment, world saves, versioned SQL with a separate runner, development migration generation | Core world catalog has no tables; no database backup or restore |
| Accounts | `AccountEntity` in the Accounts database; `IAccountService` creates, lists and verifies a login (password hash and lock state); console `account create` command, also registered for in-game administrators | Full packet-level login and realm selection; no in-game command input yet |
| Internal API | MessagePack over mutual TLS, typed request/reply handlers, channels, certificate generation | Built-in operations: a listener with no registered handlers only authenticates peers |
| Templates | `ItemTemplate` and `LootTemplate` shapes, `EnumValueSpec`, `RangeValueSpec`, TOML converters, loader contract | A loader that reads `templates/`; nothing under it is loaded |
| Plugins | Assemblies under `plugins/` registering services, commands, Lua modules, metric providers, entities and SQL | |
| Diagnostics | Periodic process metrics, plugin metric providers, snapshot events | |
| Tools | Migration runner (0.6.0); `mgboot` and `mg-uoxconv` after 0.6.0 | |
| Docker | Release image; the [one login and two game instances example](docker-login-realms.md) as a topology and roles example | That example is not a login-to-realm flow, for the reasons above |

Settings that exist only as a contract: `mode`, `network.enable_ping_server`, and
the `--log-level` and `--log-packets` command-line options are parsed and validated
but have no runtime consumer; see the
[configuration reference](server-configuration.md#settings-and-validation).
