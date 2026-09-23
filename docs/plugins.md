# Writing a plugin

A plugin is one assembly with a public class implementing `IMoongatePlugin`, dropped
under `<root>/plugins/<bundle>/`. It registers services into the host's container
before the server starts; the host resolves and starts them the same way it starts
its own built-in services.

This page takes you from an empty class library to a deployed bundle. The complete
worked example, [samples/Moongate.Sample.Plugin/](../samples/Moongate.Sample.Plugin/),
registers a persistence entity, a Lua module and enum, a console command and a metric
provider, and the test suite loads it through the real plugin loader.

## The contract

Every plugin implements `IMoongatePlugin` from `Moongate.Server.Core.Interfaces.Plugins`:

```csharp
public interface IMoongatePlugin
{
    MoongatePluginData Metadata { get; }

    void Register(Container container);
}
```

`Metadata` is a `MoongatePluginData` record constructed as
`new(id, name, version, author?, description?, dependencies?)`. `id` is the value
every dependency and error message refers to. It is matched case-insensitively and
`CODE_CONVENTION.md` §10 fixes its shape: reverse-domain,
`com.github.author.Moongate.plugins.name`. The sample uses
`com.github.moongate-community.moongate.plugins.greeter`.

`dependencies` is a list of `MoongatePluginDependencyData(id, minimumVersion?)`.
`minimumVersion` is inclusive. A list with two entries for the same ID is rejected by
the `MoongatePluginData` constructor itself, before the plugin reaches the registry.

`Register` only registers. Nothing may start, open a file or a socket, or touch the
database in it; the host starts services later, in priority order (see
[What Register may do](#what-register-may-do)).

## Creating the project

```bash
dotnet new classlib -n MyShard.Plugin
```

Then edit the generated `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Moongate.Server.Core" Version="0.6.0" ExcludeAssets="runtime" />
    <PackageReference Include="Moongate.Scripting" Version="0.6.0" ExcludeAssets="runtime" />
  </ItemGroup>

</Project>
```

Pin the version your host ships; the current one is on
[nuget.org/packages/Moongate.Server.Core](https://www.nuget.org/packages/Moongate.Server.Core).

`ExcludeAssets="runtime"` keeps each package's own `.dll` out of your build output.
The host already loads `Moongate.Server.Core.dll` and `Moongate.Scripting.dll`, so a
copy in your bundle would be dead weight. A package the host does not ship must
**not** carry that attribute, so its assembly does end up in the bundle. Plugins that
register persisted entities also reference `Moongate.Persistence` the same way; the
host supplies and identity-checks its shared FreeSql and Npgsql contracts.

`EnableDynamicLoading` makes the SDK copy your private NuGet dependencies next to the
build output; without it a dependency the host does not ship is missing from the
bundle. Name the project after the bundle you intend to deploy (`MyShard.Plugin/`
producing `plugins/MyShard.Plugin/`): the loader expects the entry assembly to carry
the bundle directory's name.

## The plugin class

The smallest useful plugin registers one console command:

```csharp
using DryIoc;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace MyShard.Plugin;

public sealed class MyShardPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new(
        "com.github.myshard.moongate.plugins.hello",
        "Hello",
        new Version(1, 0)
    );

    public void Register(Container container)
    {
        container.RegisterCommand<HelloCommand>(
            "hello",
            "Prints a greeting: hello <name>.",
            CommandSourceType.Console,
            AccountType.Regular
        );
    }
}

public sealed class HelloCommand : ICommandExecutor
{
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 1)
        {
            context.PrintError("Usage: hello <name>");

            return Task.CompletedTask;
        }

        context.Print($"Hello, {context.Arguments[0]}!");

        return Task.CompletedTask;
    }
}
```

The plugin class needs a public parameterless constructor; the loader instantiates
it before any container exists. In a real plugin, put each type in its own file.

The sample's `Register` in
[SamplePlugin.cs](../samples/Moongate.Sample.Plugin/SamplePlugin.cs) shows the
other helpers side by side: `AddPersistenceWorld<GreetingNote>()` registers a Realm
entity and its data facade, `RegisterInstance(new GreetingCounter())` shares one
instance between a Lua module and a metric provider, `AddScriptModule<GreeterModule>()`
and `RegisterScriptEnum<Tone>()` publish a Lua table and enum, and
`AddMetricProvider<GreetingMetricProvider>()` adds samples to the diagnostics
snapshot. Registration never connects to or changes the database; the host validates
every plugin registration as one batch and checks schema readiness before resolving
any startup service.

Plugins run in the configured server role. Register Auth entities and account
services only in login/standalone, and World entities, Lua world modules and game
services only in game/standalone. Persistence registration for an inactive target
fails at startup; it does not connect to the other role's database.

### Ship versioned SQL with a persistence plugin

A plugin that registers entities with `AddPersistenceAuth<T>()` or
`AddPersistenceWorld<T>()` ships its reviewed SQL beside its DLL, under
`migrations/` with a `manifest.json` holding a stable component ID, and copies that
directory to its output through a `<Content Include="migrations/**/*">` item. The
migration runner discovers the SQL without loading assemblies; core runs first, then
plugin components in ordinal order. See
[Versioned SQL files](persistence-migrations.md#versioned-sql-files) for the layout
and rules, and [Create a persistent entity](persistence-entity-tutorial.md) for a
runnable walk-through from entity class to applied migration.

## What Register may do

| Registration helper | What it registers | Documented in |
| --- | --- | --- |
| `AddMoongateService<TService, TImpl>(priority)` / `AddMoongateService<TService>(instance)` | A singleton service; if the implementation also implements `IMoongateStartupService`, it autostarts at the given `priority` and stops in reverse order. Further overloads accept factories and runtime types | this page |
| `RegisterCommand<TExecutor>(name, description, source, minimumAccountType)` | One console/in-game command executor, as a singleton | [Console commands](#console-commands) |
| `RegisterApiHandler<THandler>()` | One typed API handler singleton in the host registry; the opt-in listener freezes it after plugin loading | [API host configuration](server-configuration.md#enable-the-internal-api-server) |
| `RegisterPacketHandler<TPacket, THandler>()` | One packet handler singleton bound to an incoming packet type | this page |
| `RegisterAsyncPacketHandler<TPacket, THandler>()` | One async packet handler singleton for I/O; results return to the game loop through `PacketContext` | [Packets and handlers](packets.md#register-a-game-handler) |
| `OnEvent<TEvent>(handler)` | A `Func<TEvent, CancellationToken, Task>` subscription to one exact `IMoongateEvent` type, kept for the container's lifetime | this page |
| `AddScriptModule<T>()` / `RegisterScriptEnum<T>()` | A `[ScriptModule]` class as a singleton, published to Lua; or an enum published as a read-only global table | [Writing a Lua module](lua-modules.md) |
| `AddMetricProvider<T>()` | An `IMetricProvider` contribution, singleton, added to the diagnostics collector | [Registering a metric provider](metric-providers.md) |
| `AddPersistenceAuth<T>()` / `AddPersistenceWorld<T>()` | A typed entity facade for Accounts or Realm, with modules managed internally (needs `Moongate.Persistence`) | [PostgreSQL persistence](persistence.md) |

`priority` only matters for a service that also implements `IMoongateStartupService`:
the bootstrap starts services in ascending priority and stops them in reverse, so a
service takes a lower priority than the services that depend on it. Built-in values:

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
`IScriptEngine` (70) if it needs the engine already bound, and so on. Plugins load
before the persistence checks, and persistence schema preparation completes before
this startup-service list is resolved, regardless of a plugin service's priority.

`RegisterPacketHandler<TPacket, THandler>()` binds one `IPacketHandler<TPacket>`
singleton to one incoming packet type, called synchronously on the game loop thread;
a second handler for the same type throws before startup. The [packet guide](packets.md)
explains why handler registration alone cannot add an opcode to the wire registry.

`OnEvent<TEvent>(handler)` subscribes to the container-owned event bus from
`Register`; the handler is awaited for every published `TEvent` as long as the
container lives.

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

`Item` stands for your registered entity; `IDataAccess<T>` comes from
`Moongate.Persistence.Interfaces` and `OnEvent<TEvent>` from `Moongate.Server.Core.Extensions`.

| Event | Timing and guarantees |
| --- | --- |
| `PersistenceReadyEvent` | Plugin registration and persistence initialization have completed successfully, including connection, migration, and schema checks. Persistence is usable; startup services and `MoongateStartedEvent` follow. |
| `PersistenceStoppedEvent` | An initialized persistence owner has been disposed successfully, after service shutdown and `MoongateStoppedEvent`, but before container disposal. It uses a non-cancelable token so shutdown observers can finish. |

Both are payload-free, awaited, and emitted at most once per bootstrap lifecycle.
Nothing is published when the host has no persistence registration or initialization
fails; a later startup failure still publishes `PersistenceStoppedEvent`, which is a
closure signal, not confirmation of a final world save. Handlers run as part of the
lifecycle operation, not on the game loop; never await the bootstrap's `StartAsync` or
`StopAsync` from one. The event bus logs and isolates observer exceptions, so critical
startup validation belongs in a startup service.

## Console commands

For the built-in `echo`, `help`, `script`, and `account` commands, see
[Server commands](commands.md).

A command is a class implementing `ICommandExecutor`, registered with
`RegisterCommand<T>(name, description, source, minimumAccountType)` as in
[the plugin class](#the-plugin-class) above. `CommandContext` gives it `Arguments`
(the tokens after the command name), `Print` and `PrintError` (one output line each)
and the `CancellationToken` of the invocation.

`CommandSourceType` is a `[Flags]` enum (`InGame`, `Console`), so one command can
serve both sources by ORing them, as the built-in `echo` does. `AccountType` is
`Regular`, `GameMaster`, `Administrator` in ascending order; a console invocation is
always treated as `Administrator`, so the minimum only matters for the same command
reached from `InGame`, where the invoking session's account type is checked.

**Commands run on the thread that called the command system, never on the game loop
thread.** A command that reads or mutates game state must post its own work item to
`IGameLoopService` and await its outcome before writing the result through
`CommandContext`. The built-in `script reload` command
(`src/Moongate.Server/Commands/ScriptCommand.cs`) is the reference pattern. The
sample's [GreetCommand](../samples/Moongate.Sample.Plugin/Commands/GreetCommand.cs)
shows argument validation, including the `Enum.IsDefined` check that stops a numeric
string like `"7"` from parsing into an undefined enum member.

The built-in Ultima plugin registers the `account` command:
`account create <username> <password> [Regular|GameMaster|Administrator]`.
The level defaults to `Regular`; the interactive prompt masks the password token
and the command awaits `IAccountService.CreateAccountAsync` before reporting an outcome.
The registration also permits in-game administrators, though no in-game input is
wired yet. Its future input path must protect the password as the console does.

## Deployment and loading

Build the plugin project in Release and copy its output, everything under
`bin/Release/net10.0/` and not just the entry DLL, into `<root>/plugins/<BundleName>/`
on the target server. `<root>` is resolved at startup from `--root-directory`, then
`MOONGATE_ROOT`, then the executable's own directory.

A bundle is a directory under `<root>/plugins/`; its name is also the name the loader
expects for its entry assembly. A bundle at `plugins/mymod/` must contain `mymod.dll`,
its `mymod.deps.json`, and any private dependency the host does not already ship.
Bundles load in ordinal directory-name order; the order plugins are registered is
decided by the dependency sort, not by load order.

Each bundle gets one collectible `AssemblyLoadContext`. Its rule for every assembly
the bundle references:

1. `Moongate.Core`, `Moongate.Server.Core`, `Moongate.Persistence`,
   `Moongate.Persistence.Migrations`, `FreeSql`, `FreeSql.Provider.PostgreSQL` and
   `Npgsql` always resolve from the host, and the version, culture and public key
   token the plugin referenced must match the host's copy exactly, older or newer.
   Build against the exact package version the target host ships.
2. Every other assembly first tries the host's own load context. Everything the host
   ships (other `Moongate.*` assemblies, Serilog, DryIoc, LuaCSharp, and so on)
   resolves there as long as the version the plugin references is no newer than the
   host's. A bundled copy of such an assembly is dead weight, never an override; a
   newer reference is treated as not found and, with `ExcludeAssets="runtime"`, the
   bundle fails to load.
3. Only an assembly the host does not have falls through to the bundle: its
   `.deps.json` resolver first, then a same-named `.dll` next to the entry assembly.
   Two bundles carrying their own copies of one library get two separate instances of
   its static state.

## Errors that refuse the start

Every failure refuses the start with a named `InvalidOperationException`. Loading a
bundle wraps any failure as `Failed to load plugin bundle '{path}'.` with the original
problem as `InnerException`:

| Message | Cause |
| --- | --- |
| `The plugin entry assembly is missing.` | The bundle directory holds no DLL named after itself |
| `The assembly contains no public concrete IMoongatePlugin types.` | No plugin type in the entry assembly |
| `Plugin '{type}' needs a public parameterless constructor.` | The plugin class cannot be instantiated |
| `Required host persistence contract '{assembly}' is unavailable; private copies are not supported.` | An identity-checked assembly is missing from the host |
| `Incompatible host persistence contract '{assembly}'; host provides '{identity}'. Private copies are not supported.` | The plugin was built against a different version of an identity-checked assembly |
| `FileNotFoundException` inner exception | A dependency newer than the host's copy, with no private copy in the bundle |

The registry validates the whole batch before any `Register` runs; a rejected batch
leaves previously registered plugins untouched:

| Message | Cause |
| --- | --- |
| `Plugin ID '{id}' is already registered or duplicated.` | Two plugins share one ID, including a disk plugin colliding with a registered one |
| `Plugin '{id}' requires missing plugin '{dependency}'.` | A dependency names an ID nothing supplies |
| `Plugin '{id}' requires '{dependency}' >= {minimum}; found {version}.` | The available plugin is older than `minimumVersion` |
| `Plugin dependency cycle: first -> second -> first.` | A cycle in the dependency graph, printed as the path that closed it |
| `Plugin '{id}' failed during registration.` | The plugin's own `Register` threw; the exception is the `InnerException` |

## Testing a plugin

Test through the real loader: deploy the built bundle under a temporary
`plugins/<name>/` directory, register the host services your plugin needs in a
`Container`, add `PluginLoaderService`, and start a `MoongateServerBootstrap`. Then
resolve what your `Register` added and exercise it: call Lua through the game loop,
run commands through `CommandSystemService`, collect a diagnostics snapshot.
[tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs](../tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs)
does exactly this for the sample and is the template to copy. For unit tests that skip
the disk, construct `new MoongatePluginRegistry(container)`, call `Register(plugin)`
with an in-memory `IMoongatePlugin`, and resolve what it added from the same container.

## Common mistakes

- **Missing `EnableDynamicLoading`.** A dependency the host does not ship never
  reaches the bundle; see [Creating the project](#creating-the-project).
- **Shipping host assemblies in the bundle.** The host's own copy always wins, so a
  bundled `Moongate.*.dll` is dead weight; see
  [Deployment and loading](#deployment-and-loading).
- **Doing work, I/O or starting threads, in `Register`.** `Register` only registers;
  nothing may start yet; see [The contract](#the-contract).
- **Registering a loop-affine object and touching it from a command without
  posting.** Commands run on the caller's thread, never the game loop; see
  [Console commands](#console-commands).
- **A `[ScriptModule]` class with a constructor dependency that is not registered
  before startup.** The engine resolves the instance only when it starts; see
  [Writing a Lua module](lua-modules.md#registering).
