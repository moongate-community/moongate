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

None of this is loaded yet: `IDataLoader<ItemTemplate>` (reading every file under
`templates/items/`, resolving the `BaseId` chain across files, and registering with
`AddUltimaDataLoader`) has not been written. This page documents the mechanism once it lands;
`ItemTemplate`'s own guide follows once the loader does.
