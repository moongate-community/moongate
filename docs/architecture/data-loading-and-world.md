# Data loading and world registries

Moongate separates editable source data from runtime lookup services. Startup loaders read YAML from the configured root, seed embedded defaults when expected files or directories are absent, deserialize the data, and populate singleton registries. The item and loot template paths additionally perform explicit validation before registration. The registries are runtime stores; they are not the source files and are not persistence stores.

## Startup pipeline

`MoongateDataLoaderPlugin` registers each `IDataLoader` as a singleton together with an integer priority. `RegisterDataLoaderService` resolves the registrations in ascending priority order, and `DataLoaderService.StartAsync` awaits each loader sequentially. An exception stops the loop and propagates from startup; shutdown has no corresponding unload step.

The current order is skills (`0`), professions (`10`), locations (`20`), names (`30`), regions (`40`), weather (`50`), teleporters (`60`), containers (`70`), signs (`80`), container gumps (`90`), starting cities (`100`), titles (`110`), item templates (`120`), and loot templates (`130`). Item templates deliberately precede loot templates because loot validation resolves item ids and tags.

`FilesLoaderService` is a separate lifecycle service at priority `100`. It points the static Ultima file locator at `MoongateConfig.UltimaDirectory`, counts located client-file paths, and publishes `FilesLoadedEvent`. The data-loader pipeline is registered with lifecycle priority `110`. These numeric registrations are the visible sequencing contract; no broader event-delivery guarantee is inferred.

## YAML, defaults, and validation

Most single-file loaders register the `data` directory, write an embedded YAML default only when the target is missing, deserialize it with `YamlUtils`, and register each result. Locations use one file per facet. Item and loot templates instead seed complete embedded directory trees atomically when their respective directories do not exist, enumerate YAML recursively in case-insensitive path order, and then register records only after the whole collection validates.

The template deserializers reject malformed document shapes such as empty or null documents, duplicate keys, null elements, invalid enum values, and selected null non-nullable properties. The validators then enforce cross-record rules: unique ids, valid item relationships, and valid loot references, modes, amounts, chances, and weights. The pipeline test demonstrates that a clean root is populated with both template trees before their registries are used.

## Runtime lookup boundaries

The loaded services expose focused in-memory views rather than a general world object model:

- Skills, professions, names, titles, container definitions, container gumps, item templates, and loot templates serve their corresponding definitions.
- Locations are grouped by facet name; regions, signs, and teleporters provide map-filtered views.
- Weather profiles are keyed by id, while starting cities preserve registration order because the client uses that list index during character creation.
- `MobileFactoryService` consumes a starting-city lookup to assign a new character's map and position, falling back to the first city for an out-of-range client index.

Registration generally replaces dictionary entries sharing a key or appends to list-backed registries. The services do not advertise live file watching or reload semantics.

## Source map

### Runtime

- `src/Moongate.Server/MoongateDataLoaderPlugin.cs`
- `src/Moongate.Server/Extensions/DataLoaderRegistrationExtensions.cs`
- `src/Moongate.Server/Data/Internal/DataLoaderRegistration.cs`
- `src/Moongate.Server/Interfaces/Loading/IDataLoader.cs`
- `src/Moongate.Server/Interfaces/Loading/IDataLoaderService.cs`
- `src/Moongate.Server/Services/Loading/DataLoaderService.cs`
- `src/Moongate.Server/Services/Loading/FilesLoaderService.cs`
- `src/Moongate.Server/Loaders/ItemTemplatesLoader.cs`
- `src/Moongate.Server/Loaders/LootTemplatesLoader.cs`
- `src/Moongate.Server/Loaders/LocationsLoader.cs`
- `src/Moongate.Server/Services/Items/ItemTemplateYamlDeserializer.cs`
- `src/Moongate.Server/Services/Items/ItemTemplateValidator.cs`
- `src/Moongate.Server/Services/Items/LootTemplateYamlDeserializer.cs`
- `src/Moongate.Server/Services/Items/LootTemplateValidator.cs`
- `src/Moongate.Server/Services/World/LocationService.cs`
- `src/Moongate.Server/Services/World/RegionService.cs`
- `src/Moongate.Server/Services/World/SignService.cs`
- `src/Moongate.Server/Services/World/StartingCityService.cs`
- `src/Moongate.Server/Services/World/TeleporterService.cs`
- `src/Moongate.Server/Services/World/WeatherService.cs`
- `src/Moongate.Server/Services/Mobiles/MobileFactoryService.cs`
- `src/Moongate.UO.Data/Items/ItemTemplate.cs`
- `src/Moongate.UO.Data/Loot/LootTemplate.cs`

### Tests

- `tests/Moongate.Tests/Server/DataLoaderRegistrationTests.cs`
- `tests/Moongate.Tests/Server/DataLoaderServiceTests.cs`
- `tests/Moongate.Tests/Server/MoongateDataLoaderPluginTests.cs`
- `tests/Moongate.Tests/Server/ItemLootDataLoaderPipelineTests.cs`
- `tests/Moongate.Tests/Server/ItemTemplatesLoaderTests.cs`
- `tests/Moongate.Tests/Server/LootTemplatesLoaderTests.cs`
- `tests/Moongate.Tests/Server/LocationsLoaderTests.cs`
- `tests/Moongate.Tests/Server/RegionServiceTests.cs`
- `tests/Moongate.Tests/Server/StartingCityServiceTests.cs`
