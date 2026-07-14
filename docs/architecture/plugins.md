# Plugin boundaries

`Program.cs` asks the SquidStd bootstrap to discover plugins from the `plugins` directory and adds exactly three built-in Moongate plugins: persistence, scripting, and data loading. This page describes only those three registrations and the contracts visible in their implementations.

## Visible plugin contract

Each built-in implements `ISquidStdPlugin`, exposes `PluginMetadata`, and implements `Configure(IContainer, PluginContext)`. The metadata supplies an id, assembly-derived version, author, name, and description. Each `Configure` method adds services to the shared container; none of the three uses the supplied `PluginContext` or implements another lifecycle callback in Moongate source.

The built-ins divide composition responsibilities as follows:

- `MoongatePersistencePlugin` configures the save directory, serializer, persistence service, account/mobile entity registrations, serial generators, and default-account seeder.
- `MoongateScriptingPlugin` configures the Lua engine and Lua event bridge, then registers the `log` and `game` script modules.
- `MoongateDataLoaderPlugin` registers the client-file locator service, YAML-backed definition registries and their ordered loaders, and the data-loader lifecycle service.

`Program.cs` lists them in that order. That is a visible registration order, not evidence of a general dependency solver, plugin isolation, unload behavior, or a promise about arbitrary external plugins. Runtime start behavior documented elsewhere comes from the services each plugin registers, not from additional lifecycle methods on these plugin classes.

## Source map

### Runtime

- `src/Moongate.Server/Program.cs`
- `src/Moongate.Persistence/MoongatePersistencePlugin.cs`
- `src/Moongate.Scripting/MoongateScriptingPlugin.cs`
- `src/Moongate.Server/MoongateDataLoaderPlugin.cs`
- `src/Moongate.Server/Extensions/DataLoaderRegistrationExtensions.cs`
- `src/Moongate.Server/Services/Loading/DataLoaderService.cs`
- `src/Moongate.Server/Services/Loading/FilesLoaderService.cs`

### Tests

- `tests/Moongate.Tests/Server/MoongateDataLoaderPluginTests.cs`
- `tests/Moongate.Tests/Server/DataLoaderRegistrationTests.cs`
- `tests/Moongate.Tests/Scripting/GameLoopModuleTests.cs`

