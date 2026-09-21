# Writing a plugin

A plugin is one assembly with a public class implementing `IMoongatePlugin`, dropped
under `<root>/plugins/<bundle>/`. It registers services into the host's container
before the server starts; the host resolves and starts them the same way it starts
its own built-in services.

**What the sample does.** [samples/Moongate.Sample.Plugin/](../samples/Moongate.Sample.Plugin/)
registers five things: a world persistence entity, a shared `GreetingCounter` instance, the `greeter` Lua module
(with the `Tone` enum it takes), a `greet` console command, and a `greeter` metric
provider that reports how many greetings were produced.

## The contract

Every plugin implements `IMoongatePlugin`, quoted here from
`src/Moongate.Server.Core/Interfaces/Plugins/IMoongatePlugin.cs` (usings omitted):

```csharp
namespace Moongate.Server.Core.Interfaces.Plugins;

/// <summary>Declares plugin metadata and registers services before server startup.</summary>
public interface IMoongatePlugin
{
    /// <summary>Gets the immutable plugin metadata and required dependencies.</summary>
    MoongatePluginData Metadata { get; }

    /// <summary>Registers services in the host container without starting them.</summary>
    /// <param name="container">The host-owned dependency container.</param>
    void Register(Container container);
}
```

`Metadata` is a `MoongatePluginData`, a record whose constructor
(`src/Moongate.Server.Core/Data/Plugins/MoongatePluginData.cs`) is:

```csharp
public MoongatePluginData(
    string id,
    string name,
    Version version,
    string? author = null,
    string? description = null,
    IEnumerable<MoongatePluginDependencyData>? dependencies = null)
```

`id` is the value every dependency and error message refers to; it, and every ID
compared against it, is matched case-insensitively (`StringComparer.OrdinalIgnoreCase`
throughout `MoongatePluginRegistry`). `CODE_CONVENTION.md` §10 fixes its shape: "The
Id uses reverse-domain format, `com.github.author.Moongate.plugins.name`, and is
case-insensitive." The sample's id follows it:
`com.github.moongate-community.moongate.plugins.greeter`. `dependencies` is a list of
`MoongatePluginDependencyData`, whose constructor
(`src/Moongate.Server.Core/Data/Plugins/MoongatePluginDependencyData.cs`) is:

```csharp
public MoongatePluginDependencyData(string id, Version? minimumVersion = null)
```

`minimumVersion` is inclusive: a required plugin at exactly that version satisfies
the dependency. Passing a dependency list with two entries for the same ID is
rejected immediately, by the `MoongatePluginData` constructor itself, before the
plugin ever reaches the registry.

Plugins are ordered and validated by `MoongatePluginRegistry.ValidateAndOrder`
(`src/Moongate.Server.Core/Plugins/MoongatePluginRegistry.cs`) before any `Register`
runs. Two plugins sharing one ID — including a disk plugin that collides with one
already registered — fail the whole batch with the same wording:

```
Plugin ID '{candidate.Id}' is already registered or duplicated.
```

A dependency naming an ID nothing supplies fails with:

```
Plugin '{candidate.Id}' requires missing plugin '{dependency.Id}'.
```

and a dependency whose `minimumVersion` the available plugin does not meet fails
with:

```
Plugin '{candidate.Id}' requires '{dependency.Id}' >= {dependency.MinimumVersion}; found {required.Version}.
```

A cycle in the dependency graph fails with the path that closed it:

```
Plugin dependency cycle: {string.Join(" -> ", path.Append(candidate.Id))}.
```

which renders as the visited IDs joined by `" -> "`, for example
`Plugin dependency cycle: first -> second -> first.` for a two-plugin cycle.

None of these four checks runs any plugin's `Register`; a rejected batch leaves
every previously registered plugin untouched and lets the caller retry with a
corrected batch (`MoongatePluginRegistryTests.Register_MissingDependencyRejectsEntireBatchAndAllowsCorrection`
exercises exactly this).

## Creating the project

```bash
dotnet new classlib -n MyShard.Plugin
```

Then edit the generated `.csproj`. As an author writes it — referencing the published
packages instead of the source tree, and without the sample's `<AssemblyName>`
override (see [Deployment and loading](#deployment-and-loading) for why the sample
needs one and a real plugin usually does not) — it looks like this:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Moongate.Server.Core" Version="0.4.0" ExcludeAssets="runtime" />
    <PackageReference Include="Moongate.Scripting" Version="0.4.0" ExcludeAssets="runtime" />
  </ItemGroup>

</Project>
```

`0.4.0` is the current released version; pin the version shown on
[nuget.org/packages/Moongate.Server.Core](https://www.nuget.org/packages/Moongate.Server.Core)
or the repository's GitHub releases page.
`ExcludeAssets="runtime"` keeps each package's own `.dll` out of your
build output — the host already loads `Moongate.Server.Core.dll` and
`Moongate.Scripting.dll`, so a copy in your bundle would only be dead weight (see
[Deployment and loading](#deployment-and-loading)). A package the host does not ship
must **not** carry that attribute, so its assembly does end up in the bundle.

Plugins that register persisted entities also reference `Moongate.Persistence`
at the same version with `ExcludeAssets="runtime"`; the host supplies and
identity-checks its shared FreeSql and Npgsql persistence contracts.

`EnableDynamicLoading` controls whether the SDK copies your NuGet package
dependencies next to the build output (via `CopyLocalLockFileAssemblies`) and writes
`MyShard.Plugin.runtimeconfig.json` for a plugin host; `MyShard.Plugin.deps.json` is
written either way. Without it the SDK does not copy a private package's own `.dll`
into your output at all, so a dependency the host does not ship is simply missing
from the bundle — neither `PluginLoadContext`'s `AssemblyDependencyResolver` (built
from that `.deps.json`) nor its adjacent-file fallback (see
[Deployment and loading](#deployment-and-loading)) has anything to find.

## The plugin class

The sample's plugin class, quoted in full from
[samples/Moongate.Sample.Plugin/SamplePlugin.cs](../samples/Moongate.Sample.Plugin/SamplePlugin.cs):

```csharp
using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Sample.Plugin.Data.Persistence;
using Moongate.Sample.Plugin.Commands;
using Moongate.Sample.Plugin.Diagnostics;
using Moongate.Sample.Plugin.Internal;
using Moongate.Sample.Plugin.Modules;
using Moongate.Sample.Plugin.Types;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Sample.Plugin;

/// <summary>The sample plugin: registers a Lua module and enum, a console command, a metric provider and a persistence schema. Registration only; nothing starts here.</summary>
public sealed class SamplePlugin : IMoongatePlugin
{
    /// <inheritdoc />
    public MoongatePluginData Metadata { get; } = new(
        "com.github.moongate-community.moongate.plugins.greeter",
        "Greeter sample",
        new Version(1, 0),
        author: "Moongate",
        description: "Adds a greeter Lua module, a greet console command and a greeting counter metric."
    );

    /// <inheritdoc />
    public void Register(Container container)
    {
        container.AddPersistenceWorld<GreetingNote>();
        container.RegisterInstance(new GreetingCounter());
        container.RegisterScriptModule<GreeterModule>();
        container.RegisterScriptEnum<Tone>();
        container.RegisterCommand<GreetCommand>(
            "greet",
            "Greets someone from the console: greet <name> [tone].",
            CommandSourceType.Console,
            AccountType.Regular
        );
        container.AddMetricProvider<GreetingMetricProvider>();
    }
}
```

`AddPersistenceWorld<GreetingNote>()` selects the Realm database and registers its
asynchronous data facade. Moongate creates the internal module for the
`sample_greeter` schema from the entity's table attribute. No module class is
required, and registration does not connect or change the database. The host
validates every plugin registration as one batch, then checks schema readiness
before resolving any startup service. See [PostgreSQL persistence](persistence.md)
for attributes, schema review, transactions, and explicit complex-property mapping.
For a runnable example from the entity class through CRUD operations, follow
[Create a persistent entity](persistence-entity-tutorial.md).

`container.RegisterInstance(new GreetingCounter())` shares one counter instance
between the Lua module and the metric provider; `GreetingCounter`
(`samples/Moongate.Sample.Plugin/Internal/GreetingCounter.cs`) increments with
`Interlocked` because it is touched from the game loop (through the Lua module) and
from whatever thread runs the console command.

`container.RegisterScriptModule<GreeterModule>()` and
`container.RegisterScriptEnum<Tone>()` publish `GreeterModule`
(`samples/Moongate.Sample.Plugin/Modules/GreeterModule.cs`) as the `greeter` Lua
table and `Tone` (`samples/Moongate.Sample.Plugin/Types/Tone.cs`) as a read-only
`Tone` table; see [Registering Lua modules](#registering-lua-modules).

`container.RegisterCommand<GreetCommand>(...)` registers `GreetCommand`
(`samples/Moongate.Sample.Plugin/Commands/GreetCommand.cs`) as the `greet` console
command, restricted to `CommandSourceType.Console` and `AccountType.Regular`; see
[Console commands](#console-commands).

`container.AddMetricProvider<GreetingMetricProvider>()` adds
`GreetingMetricProvider` (`samples/Moongate.Sample.Plugin/Diagnostics/GreetingMetricProvider.cs`)
to the diagnostics collector. It reports the local name `hello_calls` from the same
counter; the diagnostics service combines that with `ProviderName` ("greeter") to
publish it in a snapshot as `greeter.hello_calls`. See
[Registering metric providers](#registering-metric-providers).

### Ship versioned SQL with a persistence plugin

Keep entity registration in `Register` with `AddPersistenceAuth<T>()` or
`AddPersistenceWorld<T>()`. Ship reviewed SQL alongside the plugin DLL:

```text
MyPlugin/
  MyPlugin.dll
  migrations/
    manifest.json
    world/0001_create_characters.sql
```

`manifest.json` contains a stable migration component ID, for example
`{ "id": "my-plugin" }`. It need not equal the plugin metadata ID; it must remain
unique and unchanged across releases and folder renames. Add to the plugin project:

```xml
<ItemGroup>
  <Content Include="migrations/**/*"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

The separate runner discovers SQL and manifests without loading assemblies.
Core runs first, then plugin component IDs in ordinal order and their numbered
scripts. Include required earlier migrations when publishing a new plugin version.
Applied files cannot change. Removing a plugin retains its schema and history.
The sample plugin includes a complete manifest, an initial World migration and
an additive migration for its column-owned Serial sequence. New notes can be
saved with a zero `Id`; `UpsertAsync` assigns the ID automatically.
Follow [Generate, review and apply](persistence.md#generate-review-and-apply).

## What Register may do

| Registration helper | What it registers | Documented in |
| --- | --- | --- |
| `RegisterMoongateService<TService, TImpl>(priority)` / `RegisterMoongateService<TService>(instance)` | A singleton service; if the implementation also implements `IMoongateStartupService`, it autostarts at the given `priority` and stops in reverse order (`src/Moongate.Server.Core/Extensions/ContainerExtensions.cs` has further overloads for factories and runtime types) | this page |
| `RegisterCommand<TExecutor>(name, description, source, minimumAccountType)` | One console/in-game command executor, as a singleton | [Console commands](#console-commands) |
| `RegisterApiHandler<THandler>()` | One typed API handler singleton in the host registry; the opt-in listener freezes it after plugin loading | [API host configuration](server-configuration.md#enable-the-internal-api-server) |
| `RegisterPacketHandler<TPacket, THandler>()` | One packet handler singleton bound to an incoming packet type | this page |
| `OnEvent<TEvent>(handler)` | A `Func<TEvent, CancellationToken, Task>` subscription to one exact `IMoongateEvent` type, kept for the container's lifetime | this page |
| `RegisterScriptModule<T>()` / `RegisterScriptEnum<T>()` | A `[ScriptModule]` class as a singleton, published to Lua; or an enum published as a read-only global table | [Registering Lua modules](#registering-lua-modules) |
| `AddMetricProvider<T>()` | An `IMetricProvider` contribution, singleton, added to the diagnostics collector | [Registering metric providers](#registering-metric-providers) |
| `AddPersistenceAuth<T>()` / `AddPersistenceWorld<T>()` | A typed entity facade for Accounts or Realm, with modules managed internally (needs `Moongate.Persistence`) | [PostgreSQL persistence](persistence.md) |

`priority` only matters for a service that also implements `IMoongateStartupService`
(`src/Moongate.Server.Core/Interfaces/Services/IMoongateStartupService.cs`): the
bootstrap starts registered services in ascending priority and stops them in
reverse, so a service takes a lower priority than the services that depend on it.
The built-in services use these priorities:

| Priority | Service |
| --- | --- |
| -900 | `TimerWheelService` |
| -800 | `IGameLoopService` (`GameLoopService`) |
| -10 | `IUltimaDataService` (`UltimaDataService`) |
| 0 (default) | `ISessionService`, `IEventBusService`, `IPluginLoaderService`, `ICommandSystemService`, and any registration that omits `priority` |
| 40 | `IWorldSaveService` (`WorldSaveService`), `IConnectionService` |
| 50 | `IPacketSendService` |
| 60 | `IPacketDispatchService` |
| 70 | `IScriptEngine` (`LuaScriptEngineService`) |
| 100 | `IGameServerService` |
| 110 | `IApiServerService` (`ApiServerService`; listener disabled by default) |
| 900 | `IDiagnosticService` (`DiagnosticService`) |
| 1000 | `IConsoleInputService` (`ConsoleInputService`) |

A plugin registering its own startup service picks a priority relative to this
table: after `IGameLoopService` (-800) if it needs to post work to the loop, after
`IScriptEngine` (70) if it needs the engine already bound, and so on.
Persistence schema preparation completes before this startup-service list is
resolved, regardless of a plugin service's numeric priority.

`RegisterPacketHandler<TPacket, THandler>()` binds one `IPacketHandler<TPacket>`
singleton (`Handle(GameSession session, TPacket packet)`,
`src/Moongate.Server.Core/Interfaces/Packets/IPacketHandler.cs`) to one incoming
packet type; the dispatcher calls it synchronously on the game loop thread, and
registering a second handler for the same packet type throws before startup.
The [packet guide](packets.md) shows the complete implementation and explains why
handler registration alone cannot add an opcode to the frozen default wire registry.
`INetworkService` is a singleton owned by the game coordinator, not an independently
autostarted listener service. See [game-loop ownership](game-loop-and-timers.md).

`OnEvent<TEvent>(handler)`, from
`src/Moongate.Server.Core/Extensions/ContainerEventExtensions.cs`, is how a plugin
subscribes to the host's event bus from `Register`:

```csharp
public Container OnEvent<TEvent>(Func<TEvent, CancellationToken, Task> handler)
    where TEvent : class, IMoongateEvent
```

It resolves (registering, if needed) the one container-owned `IMoongateEventBus` and
calls its `Subscribe`, so the handler is awaited for every published `TEvent` for as
long as the container lives.

### Persistence lifecycle events

Subscribe during `Register` to events from `Moongate.Server.Core.Data.Events`:

```csharp
container.OnEvent<PersistenceReadyEvent>(async (_, cancellationToken) =>
{
    var items = await container.Resolve<IDataAccess<Item>>()
        .GetAllAsync(cancellationToken);
    // Load plugin state before startup services begin.
});

container.OnEvent<PersistenceStoppedEvent>((_, _) =>
{
    // Persistence is disposed. Release plugin bookkeeping; do not query or save.
    return Task.CompletedTask;
});
```

`Item` stands for your registered persistence entity. Import
`Moongate.Persistence.Interfaces` for `IDataAccess<T>` and
`Moongate.Server.Core.Extensions` for `OnEvent<TEvent>`.

Both events are payload-free host lifecycle notifications published through the
same singleton event bus used by `IEventBusService`. They are awaited and emitted
at most once per bootstrap lifecycle:

| Event | Timing and guarantees |
| --- | --- |
| `PersistenceReadyEvent` | Plugin registration and persistence initialization have completed successfully, including connection, migration, and schema checks. Persistence is usable; startup services and `MoongateStartedEvent` follow. |
| `PersistenceStoppedEvent` | An initialized persistence owner has been disposed successfully, after service shutdown and `MoongateStoppedEvent`, but before container disposal. It uses a non-cancelable token so shutdown observers can finish. |

No persistence events are published when the host has no persistence registration
or initialization fails. If persistence initializes but a later startup stage
fails, cleanup still publishes `PersistenceStoppedEvent`. These events belong to
the server bootstrap; directly constructing or disposing a library persistence
owner does not publish host events. `PersistenceStoppedEvent` is a closure signal,
not confirmation that a final world save succeeded.

Handlers run as part of the lifecycle operation, not on the game loop. Do not
await the bootstrap's `StartAsync` or `StopAsync` from one of these handlers: that
operation can be waiting for the handler itself. The default event bus logs and
isolates observer exceptions; critical startup validation belongs in a startup
service. A canceled startup token still aborts startup.

### Registering Lua modules

`RegisterScriptModule<T>()` registers `T` as a container singleton and records its
type for the script engine to bind when it starts, which is why `GreetCommand` can
take `GreeterModule` in its own constructor and call the exact instance the engine
publishes to Lua; `RegisterScriptEnum<T>()` publishes an enum the same way without
requiring a module to reference it. The full authoring guide — attributes, typed
returns, the definitions file — is [docs/lua-modules.md](lua-modules.md).

### Registering metric providers

`AddMetricProvider<T>()` registers `T` as an additional singleton `IMetricProvider`;
the diagnostics collector resolves every registered provider, including plugin ones,
and folds their samples into the same snapshot and event bus described in
[docs/diagnostics.md](diagnostics.md#plugin-providers). A sample's `Name` is a local
name, not the qualified key — the collector prefixes it with the provider's own
`ProviderName` and a dot, which is why `GreetingMetricProvider` reports the local
name `hello_calls` rather than `greeter.hello_calls`. Naming rules, sample types
and failure isolation are documented in full in
[docs/metric-providers.md](metric-providers.md).

## Console commands

The sample's command, quoted in full from
[samples/Moongate.Sample.Plugin/Commands/GreetCommand.cs](../samples/Moongate.Sample.Plugin/Commands/GreetCommand.cs):

```csharp
using Moongate.Sample.Plugin.Modules;
using Moongate.Sample.Plugin.Types;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;

namespace Moongate.Sample.Plugin.Commands;

/// <summary>"greet &lt;name&gt; [plain|warm|formal]": prints a greeting on the console through the same module scripts use.</summary>
public sealed class GreetCommand : ICommandExecutor
{
    private const string Usage = "Usage: greet <name> [plain|warm|formal]";

    private readonly GreeterModule _greeter;

    /// <summary>Initializes a new instance of the <see cref="GreetCommand"/> class.</summary>
    /// <param name="greeter">The module singleton the script engine binds; its method is plain C#, safe to call from the console thread.</param>
    public GreetCommand(GreeterModule greeter)
    {
        _greeter = greeter;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length is 0 or > 2)
        {
            context.PrintError(Usage);

            return Task.CompletedTask;
        }

        var tone = Tone.Plain;

        if (context.Arguments.Length == 2 &&
            (!Enum.TryParse(context.Arguments[1], ignoreCase: true, out tone) || !Enum.IsDefined(tone)))
        {
            context.PrintError(Usage);

            return Task.CompletedTask;
        }

        context.Print(_greeter.Hello(context.Arguments[0], tone));

        return Task.CompletedTask;
    }
}
```

`Enum.TryParse` parses a numeric string like `"7"` into the enum even though no
member has that value, which is why the guard also
checks `Enum.IsDefined(tone)`: `greet Bob 7` fails that second check and prints the
usage line rather than crashing or silently picking `Tone.Plain`
(`SamplePluginTests` asserts this).

`ICommandExecutor` (`src/Moongate.Server.Core/Interfaces/Commands/ICommandExecutor.cs`)
carries this remark:

> Handlers run on the thread that called the command system, never on the game loop
> thread.
> A command that mutates game state must post its own work item to
> IGameLoopService.

`GreetCommand` never touches loop-owned state, so it needs nothing beyond calling
`_greeter.Hello(...)` directly. A command that does — reloading a script file, for
example — follows the pattern in `src/Moongate.Server/Commands/ScriptCommand.cs`:
it builds a `ScriptReloadWorkItem`, awaits `IGameLoopService.PostAsync(workItem, ...)`
to hand the work to the loop thread, and then awaits the work item's own outcome
before writing the result back through `CommandContext`.

Of `CommandContext`'s members (`src/Moongate.Server.Core/Data/Commands/CommandContext.cs`),
`GreetCommand` uses:

- `Arguments` — the whitespace-separated tokens after the command name, as
  `string[]`; `context.Arguments[0]` is the name to greet, `context.Arguments[1]` the
  optional tone.
- `Print(message, args)` — appends an informational output line.
- `PrintError(message, args)` — appends an error output line; used for every usage
  failure.

`CommandContext.CancellationToken` is the token cancelling this invocation;
`GreetCommand` does not need it, but a loop-affine command does, to pass along to
`IGameLoopService.PostAsync` and to the work item's own `WaitAsync`, exactly as
`ScriptCommand` does.

`RegisterCommand<GreetCommand>("greet", ..., CommandSourceType.Console, AccountType.Regular)`
ties the command to a source and a minimum account type.
`CommandSourceType` (`src/Moongate.Server.Core/Types/Commands/CommandSourceType.cs`)
is a `[Flags]` enum (`InGame = 1 << 0`, `Console = 1 << 1`), so a command can be
registered for more than one source by ORing them together — the built-in `echo`
command, for instance, registers with `CommandSourceType.Console | CommandSourceType.InGame`.
`AccountType` (`src/Moongate.Server.Core/Types/Accounts/AccountType.cs`) is
`Regular`, `GameMaster`, `Administrator` in ascending order; a console invocation is
always treated as `Administrator`, so `AccountType.Regular` here only matters for the
same command reached from `CommandSourceType.InGame`, where the invoking session's
own account type is checked against it.

## Deployment and loading

Build the plugin project in Release and copy its output — everything under
`bin/Release/net10.0/`, not just the entry DLL — into `<root>/plugins/<BundleName>/`
on the target server. `<root>` is the directory `Program.cs`
(`src/Moongate.Server/Program.cs`) resolves at startup: the `--root-directory`
command-line option, then the `MOONGATE_ROOT` environment variable, then the running
executable's own directory (`AppContext.BaseDirectory`) if neither is given.

A bundle is a directory under `<root>/plugins/`; its name is also the name the
loader expects for its entry assembly. `PluginLoaderService.LoadBundle`
(`src/Moongate.Server/Services/Plugins/PluginLoaderService.cs`) builds the path as
`Path.Combine(directory, Path.GetFileName(directory) + ".dll")`, so a bundle at
`plugins/mymod/` must contain `mymod.dll` and any private dependency the host does
not already ship; `mymod.deps.json` should sit alongside it too, so
`AssemblyDependencyResolver` can resolve those dependencies by name, though the
adjacent-`.dll` fallback shown below still finds a dependency placed directly in the
bundle even without one. This is also why the sample's `.csproj` sets
`<AssemblyName>SamplePlugin</AssemblyName>`: `PluginDirectoryFixture.Deploy("SamplePlugin",
"sample")` expects the built output at `PluginFixtures/SamplePlugin/SamplePlugin.dll`
so it can copy `SamplePlugin.dll`/`SamplePlugin.deps.json` into `plugins/sample/` as
`sample.dll`/`sample.deps.json`, alongside the original `SamplePlugin.*` files rather
than renaming them — a test-infrastructure naming constraint
(`PluginDirectoryFixture.cs:18,29`), not the loader's rule. An author who names the
project directory after the intended bundle name (`MyShard.Plugin/` producing
`plugins/MyShard.Plugin/`) does not need an `AssemblyName` override at all.

The loader enumerates bundle directories with `Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal)`,
so bundles load in a fixed, ordinal directory-name order; this governs load order,
not the order plugins are eventually registered, which is decided by the dependency
topological sort in `MoongatePluginRegistry.ValidateAndOrder` regardless of which
bundle loaded first.

Each bundle gets one collectible `AssemblyLoadContext`: `PluginLoadContext`
(`src/Moongate.Server/Services/Plugins/Internal/PluginLoadContext.cs`) is constructed
per bundle with `isCollectible: true`. Its `Load` override decides where every
assembly the bundle references comes from:

```csharp
protected override Assembly? Load(AssemblyName assemblyName)
{
    try
    {
        // Host contracts and their dependencies must retain the host's type identity.
        return Default.LoadFromAssemblyName(assemblyName);
    }
    catch (FileNotFoundException)
    {
        // Assemblies not supplied by the host belong to the plugin's private context.
    }

    var path = _resolver.ResolveAssemblyToPath(assemblyName);
    if (path is null && assemblyName.Name is not null)
    {
        var adjacentPath = Path.Combine(_directory, assemblyName.Name + ".dll");
        if (File.Exists(adjacentPath))
        {
            path = adjacentPath;
        }
    }

    return path is null ? null : LoadFromAssemblyPath(path);
}
```

It always tries `AssemblyLoadContext.Default` — the host's own load context — first.
Every assembly the host ships (every `Moongate.*` assembly, Serilog,
DryIoc, LuaCSharp, and so on) resolves there, so a copy of that same assembly
sitting in the bundle is not touched as long as the version the plugin references is
no newer than the host's; only `FileNotFoundException` falls through to the bundle's
own `AssemblyDependencyResolver` (built from the bundle's `.deps.json`) and, failing
that, a same-named `.dll` next to the entry assembly.

Consequences of that rule:

- Do not ship `Moongate.*.dll` in the bundle: the host's copy always wins, so a
  bundled copy is dead weight, not a working override (the sample's own bundle keeps
  four such inert files — `Moongate.Api.dll`, `Moongate.Core.dll`,
  `Moongate.Network.dll`, `Moongate.Network.Packets.dll` — pulled in transitively; see
  [Common mistakes](#common-mistakes)).
- No static state is shared between bundles for a dependency the host does *not*
  ship: each bundle's `PluginLoadContext` resolves that dependency independently, so
  two bundles carrying their own copies of the same third-party library get two
  separate instances of its static state, not one.
- A dependency the host *does* ship must be compiled against a package version no
  newer than the host you deploy to. An older reference binds cleanly to the host's
  copy. A *newer* reference is treated as not found by the host context, so the
  loader falls through to the bundle: with `ExcludeAssets="runtime"` there is no copy
  there and the bundle fails to load (`Failed to load plugin bundle '{path}'.` with an
  inner `FileNotFoundException`); if the newer `Moongate.Server.Core.dll` *is* in the
  bundle, it loads privately, the plugin's `IMoongatePlugin` is no longer the host's
  type, and the loader reports
  `The assembly contains no public concrete IMoongatePlugin types.`

`PluginLoaderService.LoadPlugins()` is called from `MoongateServerBootstrap.StartCoreAsync`
(`src/Moongate.Server/Bootstrap/MoongateServerBootstrap.cs`) as the very first step,
before `StartupServiceLifecycle.StartAsync` resolves and starts any autostart
service — but after the host's own registrations, since `RegisterServices` runs its
callback synchronously through `BootstrapLifecycleTasks.Configure` when it is
called, and `StartAsync` (and therefore `StartCoreAsync`) only runs later:

```csharp
if (_container.IsRegistered<IPluginLoaderService>())
{
    _container.Resolve<IPluginLoaderService>().LoadPlugins();
}
```

Every failure refuses the start with a named `InvalidOperationException`. Loading a
bundle wraps any failure — including the three below — as:

```
Failed to load plugin bundle '{path}'.
```

with the original problem as `InnerException`. The three loader-level problems it can
wrap are a missing entry assembly — the bundle directory holds no DLL named after
itself, the naming rule from [Deployment and loading](#deployment-and-loading):

```
The plugin entry assembly is missing.
```

no plugin type in the assembly:

```
The assembly contains no public concrete IMoongatePlugin types.
```

and a plugin type with no public parameterless constructor:

```
Plugin '{type.FullName}' needs a public parameterless constructor.
```

If loading succeeds but a plugin's own `Register` throws, `MoongatePluginRegistry.Register`
wraps it the same way:

```
Plugin '{metadata.Id}' failed during registration.
```

again with the thrown exception as `InnerException`. An unknown or under-versioned
dependency fails before any `Register` runs at all, with the wording quoted in
[The contract](#the-contract).

## Testing a plugin

The integration approach loads a real bundle through the real loader. Quoted in
full from
[tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs](../tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs):

```csharp
using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Diagnostics;
using Moongate.Tests.TestSupport.Plugins;

namespace Moongate.Tests.Integration.Plugins;

public sealed class SamplePluginTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SampleBundle_LoadsThroughTheLoader_AndItsModuleCommandAndMetricWork()
    {
        using var files = new PluginDirectoryFixture("plugins", "scripts");
        files.Deploy("SamplePlugin", "sample");
        await File.WriteAllTextAsync(Path.Combine(files.Directories["scripts"], "init.lua"),
            "greeting = greeter.hello('Moongate', Tone.Warm)\n" +
            "function report() return greeting, greeter.DEFAULT_GREETING end");
        using var container = new Container();
        container.RegisterMoongateEventBus();
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterInstance(new TimerWheelOptions());
        container.RegisterInstance(new GameLoopOptions());
        container.RegisterInstance(files.Directories);
        container.RegisterInstance(new ScriptEngineOptions { ScriptsDirectory = files.Directories["scripts"] });
        container.RegisterDelegate<ITimerService>(resolver => resolver.Resolve<TimerWheelService>(), Reuse.Singleton);
        container.RegisterMoongateService<TimerWheelService>(priority: -900)
                 .RegisterMoongateService<IGameLoopService, GameLoopService>(priority: -800)
                 .RegisterMoongateService<IEventBusService, EventBusService>()
                 .RegisterMoongateService<IPluginLoaderService, PluginLoaderService>(
                     () => new PluginLoaderService(container, files.Directories))
                 .RegisterMoongateService<IScriptEngine, LuaScriptEngineService>(LuaScriptEngineService.StartupPriority);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync().WaitAsync(Timeout);

        try
        {
            var loader = container.Resolve<IPluginLoaderService>();
            Assert.Contains(loader.Plugins, plugin => plugin.Id == "com.github.moongate-community.moongate.plugins.greeter");

            var engine = container.Resolve<IScriptEngine>();
            var loop = container.Resolve<IGameLoopService>();
            var probe = new TaskCompletionSource<object?[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            await loop.PostAsync(new ActionGameLoopWorkItem(() =>
            {
                try
                {
                    probe.SetResult(engine.Call("report").Values.ToArray());
                }
                catch (Exception exception)
                {
                    probe.SetException(exception);
                }
            }));
            Assert.Equal(["Hello there, Moongate!", "Hello"], await probe.Task.WaitAsync(Timeout));

            var commands = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
            await commands.StartAsync();
            var greeted = Assert.Single(await commands.ExecuteAsync("greet Moongate formal"));
            Assert.Equal("Good day, Moongate.", greeted.Text);
            Assert.Equal(CommandOutputLevel.Information, greeted.Level);
            var usage = Assert.Single(await commands.ExecuteAsync("greet"));
            Assert.Equal("Usage: greet <name> [plain|warm|formal]", usage.Text);
            Assert.Equal(CommandOutputLevel.Error, usage.Level);
            var undefinedTone = Assert.Single(await commands.ExecuteAsync("greet Bob 7"));
            Assert.Equal("Usage: greet <name> [plain|warm|formal]", undefinedTone.Text);
            Assert.Equal(CommandOutputLevel.Error, undefinedTone.Level);
            await commands.StopAsync();

            var provider = Assert.Single(container.ResolveMany<IMetricProvider>(), candidate => candidate.ProviderName == "greeter");
            using var diagnostics = new DiagnosticServiceFixture([provider]);
            await diagnostics.Service.StartAsync();
            var snapshot = await diagnostics.NextAsync();
            Assert.Empty(snapshot.FailedProviders);
            Assert.Equal(2, snapshot.Metrics["greeter.hello_calls"].Value);

            var definitions = await File.ReadAllTextAsync(Path.Combine(files.Directories["scripts"], "definitions.lua"));
            Assert.Contains("---@class greeter", definitions, StringComparison.Ordinal);
            Assert.Contains("---@enum Tone", definitions, StringComparison.Ordinal);
        }
        finally
        {
            await bootstrap.StopAsync().WaitAsync(Timeout);
        }
    }
}
```

This test proves three things:

- Deploying the built sample under `plugins/sample/` and calling the production
  `PluginLoaderService.LoadPlugins()` through a real `MoongateServerBootstrap` start
  proves the bundle loads exactly the way a shipped plugin would, and that
  `com.github.moongate-community.moongate.plugins.greeter` ends up in
  `IPluginLoaderService.Plugins`.
- Running `report()` on the game loop thread and then executing `greet Moongate
  formal`, the bare `greet`, and `greet Bob 7` through `CommandSystemService` proves
  the Lua module, the `Tone` enum and the console command all resolve through the
  one container the loader populated — the same `GreeterModule` instance backs both
  call sites, and both a missing argument and an undefined numeric tone (`greet Bob
  7`) are refused with the usage line rather than accepted.
- Wrapping the resolved provider in a real `DiagnosticService` through
  `DiagnosticServiceFixture` and collecting one snapshot proves the provider passes
  the same validation and naming a shipped provider would — `snapshot.FailedProviders`
  is empty, meaning `GreetingMetricProvider`'s local name `hello_calls` was accepted —
  and that the snapshot carries `greeter.hello_calls` (the service's own
  `ProviderName + "." + sample.Name` qualification) equal to `2` after exactly two
  `Hello` calls (one from Lua, one from the command), proving the shared
  `GreetingCounter` is genuinely shared. Asserting `definitions.lua` contains
  `---@class greeter` and `---@enum Tone` proves
  `RegisterScriptModule`/`RegisterScriptEnum` fed the editor tooling the plugin
  asked for.

The unit approach skips the disk and the loader and exercises the registry
directly, the way `MoongatePluginRegistryTests`
(`tests/Moongate.Tests/Server/Core/Plugins/MoongatePluginRegistryTests.cs`) does:
construct `new MoongatePluginRegistry(container)`, call `.Register(plugin)` with an
in-memory `IMoongatePlugin`, then resolve what `Register` added from the same
container. `Register_PreservesLazySingletonServiceAndRegistrationMetadata` is the
clearest example: it registers a plugin whose `Register` calls
`RegisterMoongateService<TService, TImpl>(factory, priority: 42)`, then resolves
`List<ServiceRegistrationData>` to assert the recorded priority and autostart flag,
and resolves the service itself to assert the factory ran lazily exactly once.

## Common mistakes

- **Missing `EnableDynamicLoading`.** A dependency the host does not ship never
  reaches the bundle; see [Creating the project](#creating-the-project).
- **Shipping host assemblies in the bundle.** The host's own copy always wins, so a
  bundled `Moongate.*.dll` is dead weight; see
  [Deployment and loading](#deployment-and-loading).
- **Doing work — I/O, starting threads — in `Register`.** `Register` only registers;
  nothing may start yet; see [The contract](#the-contract).
- **Registering a loop-affine object and touching it from a command without
  posting.** Commands run on the caller's thread, never the game loop; see
  [Console commands](#console-commands).
- **A `[ScriptModule]` class with a constructor dependency that is not registered
  before startup.** The engine resolves the instance only when it starts; see
  [docs/lua-modules.md#registering](lua-modules.md#registering).
