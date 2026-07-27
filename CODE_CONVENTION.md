# Code Convention — Moongate

This document defines coding conventions for the Moongate project. It is intentionally strict to keep the codebase consistent and readable.

## 1. General Principles

- Prefer clarity over cleverness.
- Keep domain boundaries explicit.
- Keep files small and focused.
- Avoid hidden magic and implicit behavior.
- Write code that is easy to reason about during debugging.

## 2. Project Structure and Namespaces

### 2.1 Folder-to-Namespace Rule

Namespace must match folder path exactly.

```
src/Moongate.Core/Services/ConfigService.cs        → namespace Moongate.Core.Services;
src/Moongate.Service/Subscribers/SocketBroadcastSubscriber.cs → namespace Moongate.Service.Subscribers;
tests/Moongate.Tests/Core/EventBusServiceTests.cs  → namespace Moongate.Tests.Core;
```

### 2.2 Domain-First Organization

Group by domain first, not by technical suffix.

### 2.3 Mandatory Namespace Buckets

| Bucket | Content |
|---|---|
| `Types` | Enums, type constants (domain-prefixed) |
| `Data` | DTOs, records, simple data carriers |
| `Data.Config` | Configuration models |
| `Data.Notifications` | Notification DTO |
| `Data.Internal.*` | Internal-only data models |
| `Interfaces` | Contracts only |
| `Services` | Service implementations |
| `Internal` | Implementation details not part of public API |
| `Subscribers` | `IEventBus` subscriber classes |
| `DBus` | D-Bus interface definitions |

## 3. C# File and Type Rules

- One `.cs` file must contain at most one primary type (`class`, `record`, or `enum`).
- File name must match type name.
- Use file-scoped namespaces.
- Do **not** use primary constructors.
- Do **not** use expression-bodied constructors (`public X(...) => ...`); constructors must always have a body `{ }`.

## 4. Class Layout Order

Inside a type, use this order:

1. `const` fields
2. `private readonly` fields (prefixed `_`)
3. Non-readonly fields
4. Properties
5. Constructor(s)
6. Public methods
7. Protected methods
8. Private methods
9. `Dispose`/finalization methods (always last)

### 4.1 Private Readonly Naming

All `private readonly` fields must start with `_`:

```csharp
private readonly IEventBus _eventBus;
private readonly DirectoriesConfig _directoriesConfig;
```

### 4.2 Dispose Position

If a class implements `IDisposable` or `IAsyncDisposable`, `Dispose`/`DisposeAsync` must be the last method(s) in the file.

## 5. Interfaces

- Interfaces live only under `Interfaces` namespaces.
- Every interface must have XML docs (`///`).
- Interface names must use `I` prefix and clear domain naming.

## 6. Enums

- Enums must live under a `Types` namespace for their domain.
- Always include the domain in the enum name.

```csharp
// Types/LogLevelType.cs
namespace Moongate.Core.Types;
public enum LogLevelType { ... }

// Types/DirectoryType.cs
namespace Moongate.Core.Types;
public enum DirectoryType { Scripts, Logs, Plugins, Configs }
```

## 7. Strings

- Empty strings: no rule. Neither `""` nor `string.Empty` is mandated — leave whichever form is
  already there, do not convert between them, and do not flag either one in review.

## 8. Logging

- Use Serilog **statically** via `Log.ForContext<T>()`. Do not inject `ILogger<T>` via DI.
- Declare the logger as a `private readonly` field initialized inline.

```csharp
private readonly ILogger _logger = Log.ForContext<MyService>();
```

- When both Serilog and Microsoft.Extensions.Logging are in scope (e.g. in `Moongate.Http.Plugin`, where ASP.NET Core brings `Microsoft.Extensions.Logging` in), add a using alias to resolve the ambiguity:

```csharp
using Serilog;
using ILogger = Serilog.ILogger;
```

- Use static message templates; never use string interpolation for structured logs.
- Keep template shape stable across calls.

## 9. Event Bus

- The event bus is `IEventBus` (`SquidStd.Core.Interfaces.Events`). Events are plain records/classes —
  there is no marker interface to implement.
- Emit with `eventBus.Publish(new SomeEvent(...))` (or `PublishAsync` when you need to await delivery);
  subscribe with `eventBus.Subscribe<SomeEvent>(handler)`, where a handler is
  `Task Handler(SomeEvent message, CancellationToken ct)`.
- Subscriber classes live in `Subscribers/` and are wired as `IEventSubscriberRegistration` (see §12);
  domain-event handlers run on the game loop.

## 10. Plugin System

- Plugins implement `ISquidStdPlugin` (`SquidStd.Plugin.Abstractions`): a `Metadata` property
  (`PluginMetadata` — a stable lowercase-dotted `Id` such as `moongate.http.plugin`, plus `Name`,
  `Version`, `Author`, `Description`, `Dependencies`) and `Configure(IContainer, PluginContext)`.
- A plugin class needs a **public parameterless constructor**. Plugins load either from the
  `plugins/` directory (`builder.FromDirectory("plugins")`) or in-tree (`builder.Add<T>()` in
  `Program.cs`), into the **default** `AssemblyLoadContext` — there is no version isolation, so a
  plugin must be built against the exact host assembly versions.
- `Configure` registers everything the plugin contributes into the DryIoc container. Seams:
  `RegisterConfigSection` / `RegisterConfigFile` and `RegisterStdService` (`SquidStd.Abstractions`);
  `RegisterCommand`, `RegisterPacketHandler`, `RegisterEventSubscriber`, `RegisterDataLoader`
  (`Moongate.Server.Abstractions.Extensions`); `RegisterApiEndpoint` (`Moongate.Http.Plugin`).
- Plugins reference `Moongate.Server.Abstractions`, **never `Moongate.Server`**. See
  `docs/contributing/writing-plugins/`.

## 11. Networking & Packets

- Packets are typed records under `Moongate.Network/Packets/` (`Incoming/`, `Outgoing/`). Each packet
  class carries `[PacketDocumentation(family, Length | IsVariableLength, SubCommand?, Name?)]` — the
  attribute is mandatory; doc generation and tests fail without it.
- Inbound handlers implement `IPacketHandler<TPacket>` **and** `IPacketHandlerRegistration`, and are
  registered with `RegisterPacketHandler<T>()`.
- After **any** change to a packet class, regenerate the packet reference from the repo root and
  commit the result: `dotnet run scripts/generate-packet-docs.cs` (writes `docs/packets/`).

## 12. Hosted Services & Event Subscribers

- Lifecycle/background services implement `ISquidStdService` (`ValueTask StartAsync` / `StopAsync`),
  registered with `RegisterStdService<TImpl, TImpl>()`. `Dispose` stays last (§4.2).
- An **optional** service must never take the server down: if a resource it needs at startup is
  unavailable (e.g. a port to bind), log a `Warning` and return cleanly rather than throwing — an
  exception out of `Configure` / `StartAsync` aborts startup.
- Event subscribers implement `IEventSubscriberRegistration` (`Subscribe(IEventBus)`), registered with
  `RegisterEventSubscriber<T>()`; they attach handlers via `eventBus.Subscribe<TEvent>(...)` and run on
  the game loop. Work started off the loop reaches world state through `IMainThreadDispatcher`.

## 13. Test Conventions

### 13.1 Structure

```
tests/Moongate.Tests/<Domain>/<Subdomain>/<SubjectName>Tests.cs
namespace Moongate.Tests.<Domain>.<Subdomain>;
```

Examples:
```
tests/Moongate.Tests/Network/PacketDocumentationTests.cs → namespace Moongate.Tests.Network;
tests/Moongate.Tests/Server/CharacterServiceTests.cs     → namespace Moongate.Tests.Server;
tests/Moongate.Tests/Support/<SharedFixture>.cs          → namespace Moongate.Tests.Support;
```

### 13.2 Naming

- File: `<SubjectName>Tests.cs`
- Class: `<SubjectName>Tests`
- One main test class per file.
- Test method style: `Method_Scenario_ExpectedResult`.

### 13.3 Test Support

- Shared fakes, builders, and helpers go in `tests/Moongate.Tests/Support/`.
- Do not mix reusable test infrastructure into domain test files.

## 14. Commits

- Use Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, etc.).
- Scope commits to the affected subsystem: `feat(console):`, `fix(network):`, `test(scripting):`.
- Never add `Co-Authored-By: Claude` to commits.

## 15. Non-Negotiable Hygiene

- No dead code.
- No TODO comments without a tracked follow-up.
- No inconsistent naming across domains.
- Keep warnings under control; do not normalize noisy warnings.
- No primary constructors.
- No expression-bodied constructors.

## 16. Additional Conventions

**Nullability**
- Use nullable reference types consistently.
- Avoid null-forgiving (`!`) unless explicitly justified.

**Async naming**
- Async methods must end with `Async`.
- Include `CancellationToken` on I/O-bound public async methods.

**Exception handling**
- Use guard clauses (`ArgumentNullException.ThrowIfNull`, etc.).
- Do not swallow exceptions silently.

**Collection exposure**
- Expose `IReadOnlyList<>` or `IReadOnlyDictionary<>` where mutation by callers is not intended.

**Test naming**
- Prefer `Method_Scenario_ExpectedResult`.
- Keep tests focused on a single behavior.

**No magic numbers**
- Replace protocol/timing literals with named constants.

**Using directives**
- Keep usings ordered: system first, then third-party, then project namespaces.
- Add using aliases when a name is ambiguous across two libraries in scope.
