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
| `RegisterCommand<TExecutor>(name, description, source, minimumAccountType, descriptionMessage)` | One console/in-game command executor, as a singleton | [Console commands](#console-commands) |
| `RegisterPacketHandler<TPacket, THandler>()` | One packet handler singleton bound to an incoming packet type | this page |
| `RegisterIncomingPacket<TPacket>()` | An incoming packet type added to the packet registry the host builds at startup, so the server can frame and decode its opcode | [Packets and handlers](packets.md#host-integration) |
| `RegisterAsyncPacketHandler<TPacket, THandler>()` | One async packet handler singleton for I/O; results return to the game loop through `PacketContext` | [Packets and handlers](packets.md#register-a-game-handler) |
| `OnEvent<TEvent>(handler)` | A `Func<TEvent, CancellationToken, Task>` subscription to one exact `IMoongateEvent` type, kept for the container's lifetime | this page |
| `AddScriptModule<T>()` / `RegisterScriptEnum<T>()` | A `[ScriptModule]` class as a singleton, published to Lua; or an enum published as a read-only global table | [Writing a Lua module](lua-modules.md) |
| `AddMetricProvider<T>()` | An `IMetricProvider` contribution, singleton, added to the diagnostics collector | [Registering a metric provider](metric-providers.md) |
| `AddConfig<T>(section)` | The plugin's own `[section]` of `config/moongate.toml`, bound to `T` and registered as a singleton | [Add a config section](#add-a-config-section) |
| `AddPersistenceAuth<T>()` / `AddPersistenceWorld<T>()` | A typed entity facade for Accounts or Realm, with modules managed internally (needs `Moongate.Persistence`) | [PostgreSQL persistence](persistence.md) |

`priority` only matters for a service that also implements `IMoongateStartupService`:
the bootstrap starts services in ascending priority and stops them in reverse, so a
service takes a lower priority than the services that depend on it. Built-in values:

| Priority | Service |
| --- | --- |
| -1000 | `RedisConnectionService` (every mode) |
| -900 | `TimerWheelService` |
| -800 | `IGameLoopService` (`GameLoopService`) |
| -10 | `IUltimaDataService` (`UltimaDataService`) |
| -5 | `IDataLoaderService` (`DataLoaderService`; game and standalone) |
| -4 | `IMapService` (`MapService`), `IMultiService` (`MultiService`); game and standalone, see [Client files and world queries](world-queries.md) |
| -3 | `IStartingItemsService` |
| 0 (default) | `ISessionService`, `IEventBusService`, `IPluginLoaderService`, `ICommandSystemService`, and any registration that omits `priority` |
| 10 to 12 | The Ultima game services: `IItemService`, `INpcService`, `IWorldPropsService` (10); `ILightService`, `IWeatherService`, `ISeasonService`, `IMusicService`, `IRegionAnnouncer`, `ItemDecayService` (11); regeneration, hunger, crime, murder, combat, guards, jail, bulletin boards, help pages, pet loyalty (12), and others |
| 40 | `IWorldSaveService` (`WorldSaveService`), `IConnectionService` |
| 50 | `IPacketSendService` |
| 55 | `IBookAttachmentService` |
| 60 | `IPacketDispatchService` |
| 70 | `IScriptEngine` (`LuaScriptEngineService`) |
| 75 to 76 | The Lua script services that need the engine bound: `NpcScriptService`, `IItemScriptService`, `IGumpScriptService`, `IEventScriptService`, `ISkillScriptService` (75); `IScheduleService`, `ISeasonalEventService`, `IItemTimerService` (76) |
| 100 | `IGameServerService` |
| 105 | `RedisRealmRegistrationService` |
| 110 | `IAdminApiService` (`AdminGrpcHostService`, from the admin plugin; listener disabled by default) |
| 900 | `IDiagnosticService` (`DiagnosticService`) |
| 1000 | `IConsoleInputService` (`ConsoleInputService`) |

A plugin registering its own startup service picks a priority relative to this
table: after `IGameLoopService` (-800) if it needs to post work to the loop, after
`IScriptEngine` (70) if it needs the engine already bound, and so on. Plugins load
before the persistence checks, and persistence schema preparation completes before
this startup-service list is resolved, regardless of a plugin service's priority.

`RegisterPacketHandler<TPacket, THandler>()` binds one `IPacketHandler<TPacket>`
singleton to one incoming packet type, called synchronously on the game loop thread;
a second handler for the same type throws before startup. A packet the plugin defines
also needs `RegisterIncomingPacket<TPacket>()`, because handler registration alone does
not add its opcode to the wire registry; see [Host integration](packets.md#host-integration).

`OnEvent<TEvent>(handler)` subscribes to the container-owned event bus from
`Register`; the handler is awaited for every published `TEvent` as long as the
container lives.

### Add a config section

A plugin keeps its settings in its own table of `config/moongate.toml`. Write a class
with the defaults, implement `IConfigSection` when some values are not allowed, and add
it at the start of `Register`:

```csharp
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Config;

public sealed class GreeterConfig : IConfigSection
{
    public string Greeting { get; set; } = "Welcome";

    public GreeterLimits Limits { get; set; } = new(); // [greeter.limits]

    public void Validate()
    {
        if (Limits.MaxPerMinute < 1)
        {
            throw new InvalidOperationException("greeter.limits.max_per_minute must be at least 1.");
        }
    }
}

public void Register(Container container)
{
    var config = container.AddConfig<GreeterConfig>("greeter");
    // Services can now take GreeterConfig in their constructor.
}
```

```toml
[greeter]
greeting = "Welcome"

[greeter.limits]
max_per_minute = 10
```

- Property names map to `snake_case` keys; a nested class is a sub-table.
- When the file has no `[greeter]` table, the plugin gets the defaults and the server
  **appends** them to the end of the file, so the operator sees every setting. The rest
  of the file is not rewritten: comments and order stay as they are. If the file cannot
  be written, a warning is logged and the defaults are used.
- `Validate()` runs after reading; an exception stops the start.
- A section name belongs to one owner: the server's own sections (`network`, `redis`,
  `persistence`, ...) and a name another plugin already added stop the start with
  `The configuration section [name] is already owned by the server or by another plugin.`
- A key with the section's name that is not a table (`greeter = 5`, `[[greeter]]`)
  stops the start: appending `[greeter]` next to it would define the key twice.
- Settings are read once, at startup; there is no reload.

**How it works.** At startup the server reads `config/moongate.toml` once, takes its
own sections, and registers the parsed file as a `ServerConfigDocument` before any
plugin's `Register` runs. `AddConfig` claims the name in that document, so no two
owners share it, then reads the table or writes the defaults. Everything happens
during registration, before any service starts: a bad value never reaches a running
server.

**Use it in a service.** The section is an ordinary singleton, so a service asks for
it in its constructor:

```csharp
public sealed class GreeterService
{
    private readonly GreeterConfig _config;

    public GreeterService(GreeterConfig config)
    {
        _config = config;
    }
}
```

A sub-table the plugin wants to hand out on its own is registered with
`container.RegisterInstance(config.Limits)`; the Ultima plugin does this so its
services receive `WorldConfig` or `CharactersConfig` rather than the whole
`UltimaConfig`.

**Test it.** A test that calls the plugin's `Register` directly registers a
`ServerConfigDocument` first, as the server does:

```csharp
var path = Path.Combine(directory, "moongate.toml");
File.WriteAllText(path, "[greeter]\ngreeting = \"Hi\"\n");
container.RegisterInstance(new ServerConfigDocument(path, TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!, []));

new GreeterPlugin().Register(container);

Assert.Equal("Hi", container.Resolve<GreeterConfig>().Greeting);
```

The Ultima plugin owns `[ultima]` and the Admin plugin owns `[admin_api]` this way.

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

For the built-in commands (`echo`, `help`, `script`, `console`, `uptime`, `version`,
`sql_backup` and `account`, plus the Ultima plugin's), see [Server commands](commands.md).
A command registered with `CommandSourceType.InGame` can also be typed in game with a
leading dot, as `.shutdown`: the Ultima plugin registers, among others, `shutdown`, `save`,
`broadcast`, `spawn`, `add`, `set`, `remove`, `hide`, `unhide`, `resurrect`, `weather`,
`season`, `time`, `go`, `gmtools`, `jail`, `pages` and `moongate`.

A command is a class implementing `ICommandExecutor`, registered with
`RegisterCommand<T>(name, description, source, minimumAccountType, descriptionMessage)` as in
[the plugin class](#the-plugin-class) above. `descriptionMessage` is optional: the id of a
message in `data/messages` that `help` shows in the server language instead of
`description`; a plugin can take ids above those Moongate uses. A command's own texts
can go through `ILocalizationService` too: take it as an optional constructor
parameter and use `localization.Text(id, english, values)`, which falls back to the
English text when a message is missing. `CommandContext` gives it `Arguments`
(the tokens after the command name), `Print` and `PrintError` (one output line each)
and the `CancellationToken` of the invocation.

A command can also implement `ICommandArgumentCompleter` to have its arguments completed by TAB on
the console: `GetArgumentCompletions(previousArguments)` gives the values the argument being typed
can take, after the arguments already typed (`[]` for the first one), or none. It runs on the
console's thread, so it returns fixed lists or names read from disk, never game state; an
exception is logged and completes nothing, and a value that is empty or holds a space is dropped,
since the command parser would split it.

```csharp
public IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments)
{
    return previousArguments.Count == 0 ? ["on", "off"] : [];
}
```

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
The registration also permits in-game administrators, who type it with a leading dot; the
`api-access` subcommand refuses in-game use.

## Deployment and loading

Build the plugin project in Release and copy its output, everything under
`bin/Release/net10.0/` and not just the entry DLL, into `<root>/plugins/<BundleName>/`
on the target server. `<root>` is resolved at startup from `--root-directory`, then
`MOONGATE_ROOT`; the server refuses to start with neither.

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
| `Plugin '{id}' failed during registration.` | The plugin's own `Register` threw, including an invalid or already-owned config section; the exception is the `InnerException` |

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

## Embedded administration plugin

`MoongateAdminPlugin` is registered in `Program.cs` after its required Ultima plugin. It ships with the server and is not loaded from `plugins/`. Its optional HTTP/2 listener uses `[admin_api]` configuration and existing host-owned services. See [Administration API](admin-api.md).
