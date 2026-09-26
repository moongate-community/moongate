# Loading TOML templates

Shard content that a designer authors by hand, such as item and mobile definitions,
is a set of TOML files under `templates/` in the server root, read once when the
shard starts. This page covers the loader contract in `Moongate.Server.Ultima`, the
TOML value types in `Moongate.Core` that make templates pleasant to write by hand,
and converter registration. It assumes [writing a plugin](plugins.md), since a
loader is registered from `Register` the same way a service or a metric provider is.

## What exists today

The loader contract, `DataLoaderService`, `EnumValueSpec<TEnum>`, `RangeValueSpec<T>`
and the converter registry are in place and tested. The `ItemTemplate` and
`LootTemplate` data shapes exist, and [a converter](uox3-migration.md) produces them
from UOX3 data. **No loader reads them yet:** `IDataLoader<ItemTemplate>` and
`IDataLoader<LootTemplate>` have not been written or registered, so template files
under `templates/` are not loaded by the current server. This page documents the
mechanism a loader plugs into; the item and loot guides follow once a loader exists.
The same contract already loads the files under `data/`, such as maps, races and
regions: see [Shard data files](data-files.md) for working loaders.
See [Implementation status](implementation-status.md).

## The loader contract

`IDataLoader<TEntity>`, in `Moongate.Server.Ultima`:

```csharp
public interface IDataLoader<TEntity>
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<DataLoaderResult<TEntity>> LoadDataAsync(CancellationToken cancellationToken = default);
}
```

`InitializeAsync` prepares the loader, opening files or connections; `LoadDataAsync`
reads everything and returns it in a `DataLoaderResult<TEntity>`, whose one property
is `IReadOnlyList<TEntity> Entities`. A loader for item templates would enumerate
every `.toml` file under its own subdirectory of `templates/` and deserialize each
with `TomlUtils`.

## Registering a loader

Call `AddUltimaDataLoader<TLoader, TEntity>` from a plugin's `Register`, the same
place services and metric providers are registered:

```csharp
container.AddUltimaDataLoader<ItemTemplateLoader, ItemTemplate>(priority: 0);
```

The loader is a singleton, reachable both by its concrete type and as
`IDataLoader<TEntity>`. The registration is appended to the list that
`DataLoaderService` runs at startup.

## Running the loaders

`DataLoaderService` is an ordinary startup service at priority `-5`, after
`IUltimaDataService` at `-10`, because a loader reading MUL or UOP files needs the
client path already configured. On `StartAsync` it runs every registered loader in
ascending priority order and keeps each result under its entity type:

```csharp
IReadOnlyList<ItemTemplate> items = dataLoaderService.GetEntities<ItemTemplate>();
```

Asking for a type nothing was registered for throws immediately, naming the type: a
missing `AddUltimaDataLoader` call fails loudly at first use, not with a silently
empty list. With no loaders registered at all, `StartAsync` still completes.

## Fields that resolve randomly

A template field is sometimes a fixed value and sometimes "pick one of these each
time an entity is created from this template". `EnumValueSpec<TEnum>` covers both
without the template type needing two fields:

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

`Resolve()` is called at the point of use, when an entity is created from the
template, not while the template is loaded: a `random_of` field gives a different
value on every spawn. It draws from `Moongate.Core.Random.BuiltInRng`, the generator
the rest of the codebase uses.

As text, the three forms are:

| TOML | Meaning |
| --- | --- |
| `rarity = "common"` | Always `Common` |
| `rarity = "random_of"` | Any member of the enum, picked fresh each `Resolve()` |
| `rarity = "random_of:rare,epic,legendary"` | One of exactly these three, picked fresh each `Resolve()` |

Parsing member names is case-insensitive; writing always lowercases them, so
`FromValue(ItemRarityType.Epic).ToString()` is `"epic"`, matching how a designer
types it. A field declares this by its type, nothing else:

```csharp
public EnumValueSpec<ItemRarityType> Rarity { get; set; } =
    EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Common);
```

## Fields that resolve to a fresh number

`RangeValueSpec<T>` is the numeric sibling, generic over `INumber<T>`:

```csharp
public readonly struct RangeValueSpec<T> where T : struct, INumber<T>
{
    public bool IsRandom { get; }

    public static RangeValueSpec<T> FromValue(T value);
    public static RangeValueSpec<T> FromRange(T min, T max);

    public T Resolve();
}
```

As text, a bare number (`hue = 1150`) is fixed; a quoted `min-max` (`hue = "1150-1200"`)
picks a fresh value in that inclusive range on every `Resolve()`. A quoted bare
number (`hue = "1150"`) is accepted too. Writing a fixed value emits a bare number;
writing a range emits the quoted form.

```csharp
public RangeValueSpec<int> Hue { get; set; } = RangeValueSpec<int>.FromValue(0);
```

## Registering a TOML converter

`EnumValueSpec<TEnum>` and `RangeValueSpec<T>` read and write through converter
factories: given any closed generic, the factory builds the matching converter by
reflection, so one factory instance covers every enum or number type a template
wraps.

`TomlUtils` keeps a global list of converters that every call without its own
explicit options picks up:

```csharp
public static void AddTomlConverter(TomlConverter converter);
public static bool RemoveTomlConverter<T>() where T : TomlConverter;
public static IReadOnlyList<TomlConverter> GetTomlConverters();
```

`AddTomlConverter` is thread-safe and idempotent: a second converter of the same
type is ignored. Registration is global and process-wide. Register once, at startup:

```csharp
TomlUtils.AddTomlConverter(new SerialTomlConverter());
TomlUtils.AddTomlConverter(new Point2DTomlConverter());
TomlUtils.AddTomlConverter(new Point3DTomlConverter());
TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
TomlUtils.AddTomlConverter(new Rectangle2DTomlConverter());
TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
```

**This never affects a call that passes its own `TomlSerializerOptions`.**
`Deserialize`, `Serialize` and the file-based overloads all take an optional
`options` parameter; when it is supplied, it is used exactly as given, with no
converters merged in from the global list.

`Rectangle2DTomlConverter` writes two corners as `"(x1, y1)..(x2, y2)"`.
The first corner is included and the second is excluded; the second point is
an endpoint, not a width and height. The old `"(x, y)+(width, height)"` syntax
is still accepted when reading. For example, both of these describe the same
rectangle, but serialization writes the first form:

```toml
bounds = "(44, 65)..(186, 159)"
# Legacy equivalent: bounds = "(44, 65)+(142, 94)"
```

Region areas use the same corner notation. `RegionAreaContent` declares its
converter through a `TomlConverter` attribute, so it needs no global registration.
See [Region areas](data-files.md#areas) for strings and optional height limits.

### Worked example: `Serial`

`Serial` is the UO wire identity, and templates name one as a graphic id:
`item_id = 0x0FEF`. `SerialTomlConverter` reads that bare hex integer, which TOML
parses natively, or the same text quoted (`item_id = "0x0FEF"`), and always writes a
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

### Worked example: `Point2D` and `Point3D`

Points are written as the quoted text their `ToString()` produces, the same form the
server prints in logs and commands, so a value copied from there pastes straight into a
file. `Point2DTomlConverter` and `Point3DTomlConverter` read and write it with the
invariant culture, so a file reads the same on every machine:

```toml
position = "(1495, 1629)"
location = "(1495, 1629, 10)"
```

Anything else, including a bare number or the wrong number of coordinates, fails with a
`TomlException` naming the offending text.

A converter for a type of your own follows the same shape: subclass
`TomlConverter<T>` for one closed type, or `TomlConverterFactory` when the type is
itself generic, and register the instance once with `TomlUtils.AddTomlConverter`.

## The template shapes

`ItemTemplate`, in `Moongate.Server.Ultima`, is a plain data shape:

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

`LootTemplate` and `LootEntry` are the same kind of shape:

| Field | Purpose |
| --- | --- |
| `LootTemplate.Id` | The stable name a `LootEntry.LootTemplateId` or an NPC's death loot names this table by |
| `LootTemplate.Comment` | A designer note nobody reads at runtime |
| `LootTemplate.Entries` | The table's weighted outcomes |
| `LootEntry.Weight` | This entry's share of the table, relative to every other entry's; `1` by default |
| `LootEntry.ItemId` | The `ItemTemplate.Id` to drop; unset when `LootTemplateId` is set instead |
| `LootEntry.LootTemplateId` | Another table's `Id` to pick from instead of a direct item |
| `LootEntry.Comment` | What `ItemId` or `LootTemplateId` is, for a human reading the file |
| `LootEntry.Amount` | `RangeValueSpec<int>`, how many of `ItemId` to create |

To produce these files from an existing UOX3 shard, see
[Migrate from UOX3](uox3-migration.md).
