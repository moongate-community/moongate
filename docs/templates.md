# Loading TOML templates

Shard content that a designer authors by hand, item definitions, mobile definitions and the like,
is a TOML file under `templates/` in the server root, loaded once when the shard starts. This page
covers the loader contract in `Moongate.Server.Ultima`, the general-purpose TOML pieces in
`Moongate.Core` that make it pleasant to author by hand, `EnumValueSpec<TEnum>`, `RangeValueSpec<T>`
and custom converter registration, and how they fit together. It assumes [writing a
plugin](plugins.md), since a loader is registered from `Register` the same way a service or a
metric provider is.

## The loader contract

`IDataLoader<TEntity>`, from `src/Moongate.Server.Ultima/Interfaces/Loaders/IDataLoader.cs`:

```csharp
public interface IDataLoader<TEntity>
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<DataLoaderResult<TEntity>> LoadDataAsync(CancellationToken cancellationToken = default);
}
```

`InitializeAsync` prepares the loader, opening files or connections; `LoadDataAsync` reads
everything and returns it wrapped in a `DataLoaderResult<TEntity>`, whose one property is
`IReadOnlyList<TEntity> Entities`. A loader for item templates would typically enumerate every
`.toml` file under its own subdirectory of `templates/` and deserialize each with
[`TomlUtils`](#registering-a-toml-converter).

## Registering a loader

`AddUltimaDataLoader<TLoader, TEntity>`, from
`src/Moongate.Server.Ultima/Extensions/DataLoaderContainerExtensions.cs`:

```csharp
public static Container AddUltimaDataLoader<TLoader, TEntity>(this Container container, int priority = 0)
    where TLoader : class, IDataLoader<TEntity>
{
    container.Register<TLoader>(Reuse.Singleton);
    container.RegisterMapping<IDataLoader<TEntity>, TLoader>();

    var registration = new DataLoaderRegistration(
        typeof(TEntity),
        typeof(TLoader),
        priority,
        async (resolver, cancellationToken) =>
        {
            var loader = resolver.Resolve<IDataLoader<TEntity>>();
            await loader.InitializeAsync(cancellationToken);
            var result = await loader.LoadDataAsync(cancellationToken);

            return result.Entities;
        }
    );

    container.AddToRegisterTypedList(registration);

    return container;
}
```

Call it from a plugin's `Register`, the same place services and metric providers are registered:

```csharp
container.AddUltimaDataLoader<ItemTemplateLoader, ItemTemplate>(priority: 0);
```

`TLoader` is registered as a singleton and mapped onto `IDataLoader<TEntity>` with
`RegisterMapping`, so resolving the loader by its concrete type and by the interface returns the
same instance; two separate `Register` calls would have produced two. The registration itself is
appended to a `List<DataLoaderRegistration>` shared by every call, the same
`container.AddToRegisterTypedList` mechanism `AddMetricProvider` and the plugin registry use for
their own typed lists.

## Running the loaders

`DataLoaderService` (`src/Moongate.Server.Ultima/Services/DataLoaderService.cs`) is an ordinary
`IMoongateStartupService`, registered like any other:

```csharp
container.AddMoongateService<IDataLoaderService, DataLoaderService>(-5);
```

Priority `-5` runs it after `IUltimaDataService` (`-10`), because a loader reading MUL or UOP
files needs `Files.SetDirectory` already pointed at the configured client path. On `StartAsync` it
resolves every registered `DataLoaderRegistration`, runs each in ascending priority order, and
keeps the result under its entity type:

```csharp
public IReadOnlyList<TEntity> GetEntities<TEntity>()
{
    if (!_entitiesByType.TryGetValue(typeof(TEntity), out var entities))
    {
        throw new InvalidOperationException($"No data loader is registered for {typeof(TEntity).Name}.");
    }

    return (IReadOnlyList<TEntity>)entities;
}
```

A type nothing was registered for throws immediately, naming the type: a missing
`AddUltimaDataLoader` call fails loudly at first use, not with a silently empty list. With zero
loaders registered at all, `StartAsync` still completes, since `DataLoaderRegistration` resolves
through `IResolverContext.Resolve<T>(IfUnresolved.ReturnDefault)` rather than throwing on an
absent registration.

## Fields that resolve randomly: `EnumValueSpec<TEnum>`

A template field is sometimes a fixed value and sometimes "pick one of these each time an entity
is created from this template." `EnumValueSpec<TEnum>`
(`src/Moongate.Core/Primitives/EnumValueSpec.cs`) covers both without the template's own type
needing two fields or a discriminated union:

```csharp
public readonly struct EnumValueSpec<TEnum> where TEnum : struct, Enum
{
    public bool IsRandom { get; }

    public static EnumValueSpec<TEnum> FromValue(TEnum value);
    public static EnumValueSpec<TEnum> FromCandidates(IReadOnlyList<TEnum> candidates);
    public static EnumValueSpec<TEnum> Random();

    public TEnum Resolve();
}
```

`Resolve()` is called at the point of use, when an entity is actually created from the template,
not while the template is being loaded: a `random_of` field gives a different value on every
spawn, not one value fixed forever at load time. It uses `Moongate.Core.Random.BuiltInRng`, the
same generator the rest of the codebase draws from.

As text, the three forms are:

| TOML | Meaning |
| --- | --- |
| `rarity = "common"` | Always `Common` |
| `rarity = "random_of"` | Any member of the enum, picked fresh each `Resolve()` |
| `rarity = "random_of:rare,epic,legendary"` | One of exactly these three, picked fresh each `Resolve()` |

Parsing member names is case-insensitive; writing always lowercases them, so
`EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Epic).ToString()` is `"epic"`, matching
how a designer would type it by hand.

A field declares this by its type, nothing else:

```csharp
public EnumValueSpec<ItemRarityType> Rarity { get; set; } =
    EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Common);
```

## Fields that resolve to a fresh number: `RangeValueSpec<T>`

The numeric sibling, for fields that are a fixed number or a range to pick a fresh value from,
generic over `INumber<T>` so it is not tied to `int`:

```csharp
public readonly struct RangeValueSpec<T> where T : struct, INumber<T>
{
    public bool IsRandom { get; }

    public static RangeValueSpec<T> FromValue(T value);
    public static RangeValueSpec<T> FromRange(T min, T max);

    public T Resolve();
}
```

As text: a bare number (`hue = 1150`), which TOML parses as a native integer literal the same way
it parses `item_id`, is fixed; a quoted `min-max` (`hue = "1150-1200"`) picks a fresh value in that
inclusive range on every `Resolve()`. A quoted bare number (`hue = "1150"`) is accepted too. Writing
a fixed value emits a bare number; writing a range emits the quoted form.

```csharp
public RangeValueSpec<int> Hue { get; set; } = RangeValueSpec<int>.FromValue(0);
```

## Registering a TOML converter

`EnumValueSpec<TEnum>` reads and writes through `EnumValueSpecTomlConverterFactory`
(`src/Moongate.Core/Serialization/Toml/EnumValueSpecTomlConverterFactory.cs`), a
`Tomlyn.Serialization.TomlConverterFactory`: given any closed `EnumValueSpec<TEnum>`, it builds
the matching `EnumValueSpecTomlConverter<TEnum>` by reflection. One factory instance therefore
covers every enum a template ever wraps in `EnumValueSpec<T>`, not one converter per enum.

`TomlUtils` (`src/Moongate.Core/Utils/TomlUtils.cs`) keeps a global list of converters that every
call without its own explicit options picks up:

```csharp
public static void AddTomlConverter(TomlConverter converter);
public static bool RemoveTomlConverter<T>() where T : TomlConverter;
public static IReadOnlyList<TomlConverter> GetTomlConverters();
```

`AddTomlConverter` is thread-safe and idempotent: a second converter of the same type is ignored.
Registration is global and process-wide, deliberately: whoever registers a converter is not
tracked, and nothing needs to be. Register once, at startup:

```csharp
TomlUtils.AddTomlConverter(new SerialTomlConverter());
TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
```

**This never affects a call that passes its own `TomlSerializerOptions`.** `Deserialize`,
`Serialize` and the file-based overloads all take an optional `options` parameter; when it is
supplied, it is used exactly as given, with no converters merged in from the global list. Only
calls that omit `options` see what was registered with `AddTomlConverter`.

### Worked example: `Serial`

`Serial` (`src/Moongate.Core/Primitives/Serial.cs`) is the UO wire identity, and templates name
one as a graphic id: `item_id = 0x0FEF`. `SerialTomlConverter`
(`src/Moongate.Core/Serialization/Toml/SerialTomlConverter.cs`) reads that bare hex integer,
which TOML parses natively, or the same text quoted, `item_id = "0x0FEF"`, and always writes a
bare integer:

```csharp
public sealed class SerialTomlConverter : TomlConverter<Serial>
{
    public override Serial Read(TomlReader reader)
    {
        if (reader.TokenType == TomlTokenType.String)
        {
            var text = reader.GetString();

            if (!Serial.TryParse(text, out var parsed))
            {
                throw reader.CreateException($"'{text}' is not a valid serial.");
            }

            return parsed;
        }

        return new Serial((uint)reader.GetInt64());
    }

    public override void Write(TomlWriter writer, Serial value)
        => writer.WriteIntegerValue(value.Value);
}
```

A converter for a type of your own follows the same shape: subclass `TomlConverter<T>` for one
closed type, or `TomlConverterFactory` when the type is itself generic, and register the instance
once with `TomlUtils.AddTomlConverter`.

## Migrate from UOX3

`src/Moongate.UoxItemConverter`, a `/tools/` project (alongside `Moongate.Boot` and
`Moongate.MigrationRunner`), converts UOX3 (github.com/UOX3DevTeam/UOX3) `.dfn` item definitions
into `ItemTemplate` TOML:

```sh
dotnet run --project src/Moongate.UoxItemConverter -- --source <file-or-directory> --destination <dir> [--loot-destination <dir>]
```

The server's own Docker image bundles the same tool, published as a self-contained single file,
at `/app/mg-uoxconv`; see [UOX3 content conversion](docker.md#uox3-content-conversion) for a
`docker run` example against a mounted UOX3 checkout when there is no local .NET SDK to hand.

Its arguments are `ConsoleApp.Run` (`ConsoleAppFramework`, the same library
`src/Moongate.Server/Program.cs` already uses) reading `Cli.Run`'s own parameters and their XML doc
comments - `--help`, `--source`/`--destination` being required while `--loot-destination` is
optional, and an unrecognized flag, all come from the framework, not from this project. A bare
invocation with no arguments at all prints the same help and exits `0`; a real mistake, some
arguments but a required one missing, exits `1`. `Cli.Run` is a thin wrapper; the real logic,
`Internal.UoxItemConverterCommand.Run`, takes plain `TextWriter`s instead of touching `Console`
directly and returns its exit code rather than setting `Environment.ExitCode`, so
`Moongate.UoxItemConverter.Tests` calls it in-process - no subprocess per test, the same shape
`Moongate.MigrationRunner.Tests` already uses for `MigrationCommand.ExecuteAsync`.

`--source` is a single `.dfn` file or a directory scanned recursively for every `.dfn` under it.
Every block from every source file is read before any `get=` chain is resolved, since a chain's
target can live in a different file than the block that names it; UOX3's own data does this, a
sword's base definition and its facing variants sit in the same file, but a shared `base_item`
often sits in another. One `<name>.toml` is written per source `.dfn`, at the same relative path
under `--destination`, holding one `[[item]]` per block that has an `id=` of its own.

UOX3's `get=` chains one item off another, `get=base_item`, or a facing variant with
`get=0x1440`, and that maps directly onto `ItemTemplate.BaseId`.

What maps, verified against real UOX3 data:

| UOX3 | ItemTemplate | Note |
| --- | --- | --- |
| The block's own `id=` | `ItemId` | Required; a block with no `id=` is not converted at all |
| The block header, or `name=` when the header is a bare hex | `Id` | Run through `StringUtils.ToSnakeCase`; `name=` is free text ("pitcher of wine") |
| `name=` | `Name` | Carried as-is; UOX3 does not separate an identifier from display text |
| A single-target `get=` | `BaseId` | Only when that target itself converted; `get=a b`, an alias with no `id=` of its own, converts nothing |
| `movable=1` | `Movable` | Anything else, including absent, is `false` |
| `color=` | `Hue` | A fixed value, not a range |
| `weightmax=` | `MaxWeight` | |

Everything else, weight, value, layer, the combat stat fields, `colorlist`, `pileable` (already
available from tiledata through `IItemCatalog`, see above), `script=`, the multi and geometry
fields, has no home in `ItemTemplate` yet and is dropped. `BaseId` is a pointer only: the
converter does not flatten a parent's fields into its children, the same way the loader itself
will resolve the chain once it exists, not before.

Every block's `Id` (and, for a `[LOOTLIST ...]` block below, its loot id) is computed once, up
front, from the block alone, before any `get=` chain or loot entry is resolved against it: a
reference to a block defined in a file scanned later in the same run still resolves.

### Verifying the output

After writing every file, the converter reads all of it back from disk, exactly as a real loader
would, and checks it: no two items or loot tables share an Id, and every `BaseId`,
`LootEntry.ItemId` and `LootEntry.LootTemplateId` names something that actually exists in what was
written. This is a real read-back, not a re-check of the resolution that already ran in memory - it
also catches a TOML round-trip going wrong, and two different headers, `Base-Item` and `base_item`
say, that only collide once both go through `ToSnakeCase`. Any problem found exits `1` and lists
every one, prefixed `Verification failed:`; a clean run prints `Verified <N> item(s) and <M> loot
table(s) read back from disk`.

### Loot tables

UOX3's `[LOOTLIST name] { ... }` blocks, real weighted loot tables verified against the engine
itself (`source/items.cpp`'s `CItem::CreateRandomItem`, not just the `.dfn` shape), convert into
`--loot-destination` (`templates/loots/`, next to `templates/items/`) as one `<id>.toml` per table,
named after the table's own Id, not the source `.dfn`'s: real UOX3 data defines all 71 tables in
one file, `lootlists.dfn`, and reviewing one has no reason to load every other table alongside it.
Without `--loot-destination`, every `LOOTLIST` block converts nothing, the same as before this
converter knew about loot at all. Each bare entry line is:

```
weight|entry[,amount]
```

`weight` defaults to `1` when the `weight|` prefix is absent. `entry` is an item header (resolved
through the same map `get=` uses), `LOOTLIST=other` (a nested, weighted pick from another table,
only once `other` is confirmed to be a real table), or the literal `blank`, a real weighted chance
of dropping nothing. `amount` is a single count or `min max` (a space, not a dash) and maps onto
`LootEntry.Amount`, a `RangeValueSpec<int>`.

| UOX3 | LootTemplate / LootEntry | Note |
| --- | --- | --- |
| The block header's name, after `LOOTLIST ` | `LootTemplate.Id` | Also through `StringUtils.ToSnakeCase`; real names are camelCase ("eartheleLoot") |
| An entry's `weight\|` prefix | `LootEntry.Weight` | Defaults to `1` |
| An item header entry | `LootEntry.ItemId` | Resolved through the same map `get=` uses; also fills `Comment` with that item's own `name=`, when it had one |
| `LOOTLIST=other` | `LootEntry.LootTemplateId` | Only when `other` itself converted |
| `blank` | Neither `ItemId` nor `LootTemplateId` set | A real, weighted chance of nothing |
| A trailing `,amount` | `LootEntry.Amount` | `RangeValueSpec<int>`; `min max` (space) becomes a range |

`ITEMLIST=`, UOX3's "spawn every entry" sibling to `LOOTLIST=`, has no home in `LootEntry` (it is
a different mechanic, not a weighted pick) and never appears in real `lootlists.dfn` data. An
entry the converter cannot resolve any other way is dropped, same as an unresolved `get=`.

A trailing `//comment` is stripped from every line before anything else, matching the real
engine's own `oldstrutil::removeTrailing(sLine, "//")`: real data glues one straight onto a block's
opening brace with no space (`{//approximately 1%`), which would otherwise hide the whole block,
not just the comment.

## What is not built yet

The loader contract, `DataLoaderService`, `EnumValueSpec<TEnum>`, `RangeValueSpec<T>` and the
converter registry are all in place and tested. `ItemTemplate`
(`src/Moongate.Server.Ultima/Data/Templates/Items/ItemTemplate.cs`) exists as a plain data shape:

| Field | Purpose |
| --- | --- |
| `Id` | The stable name a loot table, a spawn or `additem` names this template by |
| `BaseId` | Another template's `Id` to inherit unset fields from; the loader resolves the chain |
| `ItemId` | The base client graphic; physical properties come from `IItemCatalog`, not this type |
| `Name`, `Comment` | A display name override, and a designer note nobody reads at runtime |
| `Rarity` | `EnumValueSpec<ItemRarityType>` |
| `ScriptId` | Names the Lua module handling this template's behaviour |
| `Movable` | Tiledata carries no such flag, so this is explicit |
| `Hue` | `RangeValueSpec<int>`, `0` meaning the art's native coloring |
| `MaxItems`, `MaxWeight` | Nullable; set only on a container template |

`LootTemplate`/`LootEntry`
(`src/Moongate.Server.Ultima/Data/Templates/Items/{LootTemplate,LootEntry}.cs`) are the same:

| Field | Purpose |
| --- | --- |
| `LootTemplate.Id` | The stable name a `LootEntry.LootTemplateId` or an NPC's death loot names this table by |
| `LootTemplate.Comment` | A designer note nobody reads at runtime |
| `LootTemplate.Entries` | The table's weighted outcomes |
| `LootEntry.Weight` | This entry's share of the table, relative to every other entry's; `1` by default |
| `LootEntry.ItemId` | The `ItemTemplate.Id` to drop; unset when `LootTemplateId` is set instead |
| `LootEntry.LootTemplateId` | Another table's `Id` to pick from instead of a direct item |
| `LootEntry.Comment` | What `ItemId`/`LootTemplateId` is, for a human reading this file by hand; an id alone says nothing |
| `LootEntry.Amount` | `RangeValueSpec<int>`, how many of `ItemId` to create |

None of this is loaded yet: `IDataLoader<ItemTemplate>` (reading every file under
`templates/items/`, resolving the `BaseId` chain across files) and `IDataLoader<LootTemplate>`
(reading `templates/loots/`, resolving `LootEntry` references against both) have not been
written, nor has either been registered with `AddUltimaDataLoader`. This page documents the
mechanism once it lands; `ItemTemplate`'s and `LootTemplate`'s own guides follow once the loader
does.
