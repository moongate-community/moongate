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
src/Moongate.Core/Geometry/Point3D.cs                          → namespace Moongate.Core.Geometry;
src/Moongate.Server/Services/Persistence/WorldSaveService.cs   → namespace Moongate.Server.Services.Persistence;
tests/Moongate.Tests/Server/Services/Events/EventBusServiceTests.cs → namespace Moongate.Tests.Server.Services.Events;
```

### 2.2 Domain-First Organization

Group by domain first, not by technical suffix.

### 2.3 Mandatory Namespace Buckets

| Bucket | Content |
|---|---|
| `Types` | Enums, type constants (domain-prefixed) |
| `Data` | DTOs, records, simple data carriers |
| `Data.Config` | Configuration models |
| `Data.Internal.*` | Internal-only data models |
| `Interfaces` | Contracts only |
| `Services` | Service implementations |
| `Extensions` | Extension members, grouped by the type they extend |
| `Attributes` | Custom attributes |
| `Internal` | Implementation details not part of public API |

Each bucket takes a domain subfolder once it holds more than a handful of types: `Types/Accounts`,
`Data/Sessions`, `Interfaces/Services`, `Extensions/Strings`.

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

- Enums must live under a `Types` namespace, in the subfolder of the domain they belong to.
- Always include the domain in the enum name.

```csharp
// Types/LogLevelType.cs
namespace Moongate.Core.Types;
public enum LogLevelType { ... }

// Types/Accounts/AccountType.cs
namespace Moongate.Server.Core.Types.Accounts;
public enum AccountType { Regular = 0, GameMaster = 1, Administrator = 2 }
```

- A flags enum gives every member an explicit value and declares a zero member, so that
  `HasFlag` cannot answer true for an unset value.

## 7. Strings

- Empty strings have no imposed form. Neither `""` nor `string.Empty` is the standard: leave whichever
  a file already uses and do not convert in either direction.

## 8. Logging

- Use Serilog **statically** via `Log.ForContext<T>()`. Do not inject `ILogger<T>` via DI.
- Declare the logger as a `private readonly` field initialized inline.

```csharp
private readonly ILogger _logger = Log.ForContext<MyService>();
```

- Where a project namespace shadows a framework type, qualify or alias rather than renaming the namespace.
  `Moongate.Server.Services.Console` shadows `System.Console`, so that code writes `System.Console.WriteLine`.

- Use static message templates; never use string interpolation for structured logs.
- Keep template shape stable across calls.

## 9. Event Bus

- All event types must implement `IMoongateEvent`.
- `IMoongateEventBus` declares the bus: `Subscribe<TEvent>` returns an idempotent `IDisposable`, and
  `PublishAsync<TEvent>` emits. Services inject `IEventBusService`, which is that contract plus
  `IMoongateService`, so both reach the one container-owned bus.
- Handlers run sequentially in registration order and the publisher awaits each. One failing handler is
  logged and does not skip the handlers after it. Events are routed by exact type, and a late subscriber
  never receives an earlier event.

```csharp
internal sealed class MySubscriber
{
    public MySubscriber(IEventBusService eventBus)
    {
        eventBus.Subscribe<MoongateStartedEvent>(HandleAsync);
    }
}
```

- Plugins subscribe from `Register` with `container.OnEvent<TEvent>(handler)`, which the container owns
  for the lifetime of the host.

## 10. Plugin System

- Plugins implement `IMoongatePlugin`: a `MoongatePluginData Metadata` property and `void Register(Container)`.
  The Id uses reverse-domain format, `com.github.author.Moongate.plugins.name`, and is case-insensitive.
- `Register` registers services and event subscriptions. It never starts them; the host owns the container
  and the service lifecycle.
- Declare dependencies in `MoongatePluginData`. They are required, and a batch is validated and ordered
  before any `Register` callback runs, so duplicate ids, missing dependencies and cycles reject the whole
  batch rather than leaving half of it applied.
- Disk plugins live one bundle per directory under `plugins/`, where the directory name matches the entry
  assembly. Each bundle gets its own collectible load context; host assemblies are shared to preserve
  contract identity, and the rest resolve privately.

## 11. Startup Services / Subscribers

- Services that own work across the host lifetime implement `IMoongateStartupService`, which extends `IMoongateService` with `StartAsync()` and `StopAsync()`.
- Register them with `container.RegisterMoongateService<TContract, TService>(priority)`. Services start in ascending priority order and stop in reverse, so a dependency takes a lower number than its dependents; the default is `0`.
- If an optional dependency is unavailable at startup, log a `Warning` and return cleanly — do not bring the host down. Throw only when the server cannot run without it; the bootstrap then stops every service it already started, in reverse order.
- Subscribers that are not startup services are registered as singletons and force-resolved in `Program.cs` to trigger constructor subscription registration.

## 12. Test Conventions

### 12.1 Structure

```
tests/Moongate.Tests/<Domain>/<Subdomain>/<SubjectName>Tests.cs
namespace Moongate.Tests.<Domain>.<Subdomain>;
```

Examples:
```
tests/Moongate.Tests/Core/Geometry/Point3DTests.cs                  → namespace Moongate.Tests.Core.Geometry;
tests/Moongate.Tests/Server/Services/Events/EventBusServiceTests.cs → namespace Moongate.Tests.Server.Services.Events;
tests/Moongate.Tests/TestSupport/Persistence/WorldSaveFixture.cs    → namespace Moongate.Tests.TestSupport.Persistence;
```

Integration, contract and performance tests go in their own folder rather than beside unit tests:
`tests/Moongate.Tests/Integration/<Domain>/`. Nothing lives in the test project root.

### 12.2 Naming

- File: `<SubjectName>Tests.cs`
- Class: `<SubjectName>Tests`
- One main test class per file.
- Test method style: `Method_Scenario_ExpectedResult`.

### 12.3 Test Support

- Shared fakes, builders, and fixtures go in `tests/Moongate.Tests/TestSupport/<Domain>/`, mirroring the
  domain layout of the tests that use them.
- `tests/Moongate.Tests/Support/` is the older location and still holds part of this material. Put new
  helpers in `TestSupport/`, and move an existing one when you are already editing it.
- Do not mix reusable test infrastructure into domain test files.

### 12.4 InternalsVisibleTo

Projects expose internals to their own test project:

```xml
<InternalsVisibleTo Include="Moongate.Tests" />
```

`Moongate.Persistence`, `Moongate.Server` and `Moongate.Ultima` grant it to `Moongate.Tests`;
`Moongate.Network` grants it to `Moongate.Network.Tests`.

## 13. Branching and Commits

### 13.1 Feature Workflow

Every feature takes the same three steps, in this order. None of them is optional.

1. **Open an issue first.** It describes the feature in detail: what it does, why it is wanted, how it
   behaves at its edges, and how it will be verified. The issue is the specification the work is judged
   against, so a title and a sentence are not an issue.
2. **Branch from `develop`**, named `feature/<short-name>`, carrying that one feature and nothing else.
3. **Open a pull request into `develop`** and link the issue. Work reaches `develop` through that
   request, never through a direct push.

Fixes follow the same path under `fix/<short-name>`. `main` receives `develop` at release time and takes
nothing else.

### 13.2 Commit Messages

- Use Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, etc.). Release versions are
  derived from them, so the type and any `!` breaking marker decide the next version number.
- Scope commits to the affected subsystem: `feat(persistence):`, `fix(network):`, `test(eventbus):`.
- Write every commit in English, subject and body alike.
- Never add AI attribution anywhere: no `Co-Authored-By: Claude` trailer, and no generated-with line in a
  commit, pull request, issue or release note.

## 14. Non-Negotiable Hygiene

- No dead code.
- No TODO comments without a tracked follow-up.
- No inconsistent naming across domains.
- Keep warnings under control; do not normalize noisy warnings. The build is at zero warnings, so a new
  one is a regression.
- No primary constructors.
- No expression-bodied constructors.

## 15. Additional Conventions

**Nullability**
- Use nullable reference types consistently.
- Avoid null-forgiving (`!`) unless explicitly justified.

**Async naming**
- Async methods must end with `Async`.
- Include `CancellationToken` on I/O-bound public async methods.

**Exception handling**
- Guard the arguments of public API surface with `ArgumentNullException.ThrowIfNull` and friends.
- Do **not** guard constructor dependencies that arrive from the container: let the container fail on a
  missing registration instead of repeating the check in every service.
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
