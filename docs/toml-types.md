# TOML value types

Moongate reads its configuration, its data files and its templates from TOML. Most
fields are plain TOML values: strings, integers, booleans, arrays and tables. Some
fields have a C# type that TOML does not know, such as a point, a hue range or an
account type. A converter translates between that type and a TOML value: it reads
the value when a file is loaded and writes it when code saves a file.

This page lists every converter, the forms it accepts, the form it writes and the
errors it gives. Key names are always snake_case: the property `GoLocation` is the
key `go_location`.

## How a converter is applied

A converter reaches a field in one of three ways.

**Built in for enums.** `TomlUtils` always adds `EnumTomlConverterFactory` to its
default options, so every enum is written and read by name with no registration:
see [Enums](#enums). A converter registered for one enum type wins over it.

**Registered for every call.** `TomlUtils` in `Moongate.Core` keeps a list of
converters that every call without its own options uses:

```csharp
public static void AddTomlConverter(TomlConverter converter);
public static bool RemoveTomlConverter<T>() where T : TomlConverter;
public static IReadOnlyList<TomlConverter> GetTomlConverters();
```

`AddTomlConverter` ignores a second converter of the same type.
`RemoveTomlConverter<T>` removes every converter of type `T` and returns whether it
removed one. `GetTomlConverters` returns the current list. Both changes are
thread-safe: each one builds a new list and new default options under a lock and
publishes them together, so a call running at the same time sees the old set or the
new one, never a mix. The list is global to the process, so register once, at
startup.

These classes register converters:

| Who | Converters |
| --- | --- |
| `MoongateUltimaPlugin.Register` | `Serial`, `Point2D`, `Point3D`, `HueSpec`, `Rectangle2D`, and the `EnumValueSpec` and `RangeValueSpec` factories |
| `Moongate.UoxItemConverter` | the same, except `Rectangle2D` |
| Tests | none globally; each test passes its own options with the converter it checks |

A registered converter also covers the nullable form of its type: `go_location` in
a region file is a `Point3D?` and uses `Point3DTomlConverter`.

**Declared with an attribute.** `[TomlConverter(typeof(...))]` on a property or on a
type applies the converter to that property, or to every use of that type, with no
registration. Region areas use it on their type:

```csharp
[TomlConverter(typeof(RegionAreaContentTomlConverter))]
public sealed class RegionAreaContent
```

A converter named by an attribute must convert exactly the member type: an `int?`
property needs a converter for `int?`, not for `int`. Registered converters and the
built-in enum converter have no such limit: they also cover the nullable form, and
an unset nullable value is left out of the file.

**Explicit options get nothing.** `Deserialize`, `Serialize` and the file overloads
take an optional `TomlSerializerOptions`. Without it, the call uses the default
options: snake_case names, the registered converters and the enum converter. With it, the call uses
the options exactly as given: registered converters are never added to them.
Converters declared with an attribute still apply, because they belong to the type.

When a converter rejects a value, loading fails with a `TomlException`. The message
gives the line and column and ends with the converter's reason, for example:

```text
(1,10) : error : Exception while trying to convert TOML value to type '...EnumValueSpec`1[...]' using converter '...'.
Reason: (1,10) : error : 'not-a-member' is not a valid ItemRarityType value or random_of spec.
```

The reasons quoted on this page are that last part.

## Summary

| Type | TOML form | Example | Converter | Applied by |
| --- | --- | --- | --- | --- |
| `Serial` | bare integer, or a quoted number | `item_id = 0x0FEF` | `SerialTomlConverter` | registration |
| `Point2D` | quoted `"(x, y)"` | `size = "(7168, 4096)"` | `Point2DTomlConverter` | registration |
| `Point3D` | quoted `"(x, y, z)"` | `location = "(3503, 2574, 14)"` | `Point3DTomlConverter` | registration |
| `Rectangle2D` | quoted `"(x1, y1)..(x2, y2)"` | `bounds = "(44, 65)..(186, 159)"` | `Rectangle2DTomlConverter` | registration |
| `RegionAreaContent` | a rectangle string, or a table with `bounds`, `z1`, `z2` | `{ bounds = "(1, 2)..(3, 4)", z1 = 0 }` | `RegionAreaContentTomlConverter` | attribute on the type |
| `HueSpec` | bare integer, or a quoted hue or range | `hue = "0x047E-0x04B0"` | `HueSpecTomlConverter` | registration |
| `EnumValueSpec<TEnum>` | quoted member name or `random_of` spec | `rarity = "random_of:rare,epic"` | `EnumValueSpecTomlConverterFactory` | registration |
| `RangeValueSpec<T>` | bare number, or a quoted `"min-max"` | `amount = "5-10"` | `RangeValueSpecTomlConverterFactory` | registration |
| `decimal` | bare integer or float | `weight = 0.02`, `weight = 7` | built in | built in |
| `DiceSpec` | bare integer, or a quoted dice expression | `strength = "1d25+95"` | `DiceSpecTomlConverter` | registration |
| Any enum | its snake_case name; flags joined by `\|` | `visibility = "game_master"`, `mode = "standalone"` | `EnumTomlConverterFactory` | built in |

The converters of the first group and the enum converter are in
`Moongate.Core/Serialization/Toml`; `RegionAreaContentTomlConverter` is in
`Moongate.Server.Ultima/Serialization/Toml`.

## Serial

A `Serial` is a UO identity: an entity serial, a graphic id or a cliloc number.

Accepted forms:

```toml
item_id = 0x0FEF    # any TOML integer: decimal, hex, octal or binary
item_id = 4079      # the same value
item_id = "0x0FEF"  # quoted hex
item_id = "4079"    # quoted decimal
```

In quoted text, the `0x` prefix picks the base: `"40000001"` is forty million, not
the first item serial `"0x40000001"`. Quoted text takes no sign; spaces around it
are ignored.

Written form: always a bare decimal integer. `0x0FEF` is written back as `4079`.

Errors:

| Value | Result |
| --- | --- |
| `item_id = "not-a-serial"` | `'not-a-serial' is not a valid serial.` |
| `item_id = 1.5` | `Expected token Integer but was Float.` |
| `item_id = -1` | no error: a bare integer is not range-checked and is cut to 32 bits, so `-1` becomes `0xFFFFFFFF` |

Used by: `cliloc` in `starting_cities.toml`, and `item_id` in item templates.

## Point2D and Point3D

A point is written as the quoted text its `ToString()` produces, the same form the
server prints in logs and commands, so a value copied from there pastes into a file.

Accepted forms:

```toml
size = "(7168, 4096)"             # Point2D: (x, y)
location = "(1495, 1629, 10)"     # Point3D: (x, y, z)
location = "( -5 , 7 , -20 )"     # spaces are ignored; negative numbers are allowed
```

The parentheses are required and every coordinate is an integer. Both converters
use the invariant culture, so a file reads the same on every machine and a negative
number is always written with an ASCII minus.

Written form: `"(x, y)"` or `"(x, y, z)"`, with one space after each comma.

Errors (shown for `Point2D`; `Point3D` gives the same messages with `(x, y, z)`):

| Value | Result |
| --- | --- |
| `position = 5` | `Expected a "(x, y)" string for a Point2D.` |
| `position = "not-a-point"` | `'not-a-point' is not a valid Point2D, expected "(x, y)".` |
| `position = "(1, 2, 3)"` | `'(1, 2, 3)' is not a valid Point2D, expected "(x, y)".` |
| `location = "(1, 2)"` | `'(1, 2)' is not a valid Point3D, expected "(x, y, z)".` |

Used by: `size` in `maps.toml` (`Point2D`); `location` in `starting_cities.toml`,
`go_location` and `entrance` in region files (`Point3D`).

## Rectangle2D

A rectangle is two corners joined by `..`. The first corner is included and the
second is excluded: the second point is an end, not a width and height.

Accepted forms:

```toml
bounds = "(44, 65)..(186, 159)"       # X 44 to 185, Y 65 to 158
bounds = " (44, 65) .. (186, 159) "   # spaces are ignored
bounds = "(44, 65)+(142, 94)"         # legacy corner-plus-size form, the same rectangle
```

Written form: always the corner form, `"(44, 65)..(186, 159)"`. A file that uses the
legacy form is rewritten in the corner form the next time code saves it.

Errors:

| Value | Result |
| --- | --- |
| `bounds = 5` | `Expected a "(x1, y1)..(x2, y2)" string for a Rectangle2D.` |
| `bounds = "(44, 65)"` | `'(44, 65)' is not a valid Rectangle2D, expected "(x1, y1)..(x2, y2)".` |
| `bounds = "(44, 65)-(142, 94)"` | the same message, naming `(44, 65)-(142, 94)` |
| `bounds = "44 65 142 94"` | the same message, naming `44 65 142 94` |

Text that contains `..` but whose corners do not parse is always an error; it is
never tried as the legacy form.

Used by: `bounds` in `containers.toml`, and by the region area converter below.

## Region areas

Each entry of `areas` in a region file is a `RegionAreaContent`. The type carries
`[TomlConverter(typeof(RegionAreaContentTomlConverter))]`, so it needs no
registration, and it reads its rectangle with its own `Rectangle2DTomlConverter`.
The start is included and the end is excluded on every axis, height too.

Accepted forms, which may be mixed in one array:

```toml
areas = [
    "(1330, 1991)..(1343, 2004)",                              # every height
    { bounds = "(1416, 1498)..(1740, 1777)", z1 = -10, z2 = 128 },
    { bounds = "(1, 2)..(3, 4)", z1 = 0 },                     # either limit may be left out
    { x1 = 10, y1 = 20, x2 = 30, y2 = 40, z2 = -80 },          # legacy corners, read only
]
```

A string takes every form `Rectangle2D` accepts, including the legacy `+` form.
Inside a table, the keys are `bounds`, `x1`, `y1`, `x2`, `y2`, `z1` and `z2`; `z1`
is included and `z2` is excluded.

Written form: a string when the area has no height limit, otherwise an inline table
with `bounds` and the limits it has. The legacy `x1`/`y1`/`x2`/`y2` keys are never
written.

Errors:

| Value | Result |
| --- | --- |
| `42` | `Expected a region area string or a table with bounds and optional z1/z2.` |
| `"(1, 2)..bad"` | the `Rectangle2D` message |
| `{ bounds = "(1, 2)..(3, 4)", x1 = 1 }` | `Use bounds or x1/y1/x2/y2 for a region area, not both.` |
| `{ z1 = 0 }` or `{ x1 = 1, y1 = 2, x2 = 3 }` | `A region area table requires bounds or all of x1/y1/x2/y2.` |
| `{ bounds = "(1, 2)..(3, 4)", z1 = 1.5 }` | `Region area coordinates must be integers.` |
| `{ bounds = "(1, 2)..(3, 4)", z2 = 2147483648 }` | `Region area coordinates must fit in a 32-bit integer.` |
| `{ bounds = "(1, 2)..(3, 4)", top = 5 }` | `Unknown region area field 'top'.` |

Used by: `areas` in `data/regions/<map>.toml`. See
[Region areas](data-files.md#areas).

## HueSpec

A `HueSpec` is either one hue or a range to pick a fresh hue from each time it is
resolved, for example once per spawned item. Every hue is between `0` and `0xFFFF`.

Accepted forms:

```toml
hue = 1150                  # bare integer, decimal
hue = 0x047E                # bare integer, hex: the same hue
hue = "0x047E"              # a quoted hue, decimal or hex
hue = "1150-1200"           # a range, both bounds included
hue = "0x047E-0x04B0"       # the same range in hex
hue = "hue(1150:1200)"      # the same range, alternative form
```

In a range, the first bound must not be greater than the second. A range with equal
bounds is accepted and is still a range.

Written form: a single hue as a bare decimal integer (`hue = 1150`), a range as a
quoted hex range (`hue = "0x047E-0x04B0"`).

Errors:

| Value | Result |
| --- | --- |
| `hue = 70000` | `70000 is not a hue; a hue must be between 0 and 0xFFFF.` |
| `hue = -1` | `-1 is not a hue; a hue must be between 0 and 0xFFFF.` |
| `hue = "red"` | `'red' is not a hue or a hue range.` |
| `hue = "1200-1150"` | `'1200-1150' is not a hue or a hue range.` |
| `hue = 1.5` | `Expected a hue number or a hue range string.` |

Used by: `skin_hues` and `hair_hues` in `races.toml` (arrays of `HueSpec`, such as
`["0x03EA-0x0422"]`), and `hue` in item templates.

## EnumValueSpec

An `EnumValueSpec<TEnum>` is an enum field that is either fixed or picked at random
each time it is resolved. One registration of `EnumValueSpecTomlConverterFactory`
covers every enum: the factory builds an `EnumValueSpecTomlConverter<TEnum>` for
each `EnumValueSpec<TEnum>` it meets.

Accepted forms, always a quoted string:

```toml
rarity = "common"                          # always Common
rarity = "random_of"                       # any member, picked on each resolve
rarity = "random_of:rare,epic,legendary"   # one of these members, picked on each resolve
```

Member names ignore case and underscores: `"Epic"` and `"epic"` are the same, and so
are `"north_east"`, `"NorthEast"` and `"northeast"`. Spaces around the names in the
list are ignored. Only names are accepted, never numbers.

Written form: lowercase snake_case, `"rare"`, `"north_east"` or
`"random_of:rare,epic"`; it always reads back.

Errors:

| Value | Result |
| --- | --- |
| `rarity = "not-a-member"` | `'not-a-member' is not a valid ItemRarityType value or random_of spec.` |
| `rarity = "random_of:"` | the same message, naming `random_of:` |
| `rarity = "3"` | `'3' is not a valid ItemRarityType value or random_of spec.` |
| `rarity = 1` | `Expected token String but was Integer.` |

Used by: `rarity` in item templates. See
[Fields that resolve randomly](templates.md#fields-that-resolve-randomly).

## RangeValueSpec

A `RangeValueSpec<T>` is a number field that is either fixed or picked from a range
each time it is resolved. `T` is any .NET number type. One registration of
`RangeValueSpecTomlConverterFactory` covers every number type.

Accepted forms:

```toml
amount = 5         # bare integer: fixed
amount = 2.5       # bare float: fixed
amount = "5"       # quoted number: fixed
amount = "5-10"    # quoted range: a fresh value from 5 to 10 on each resolve
amount = "-10--5"  # negative bounds: a leading minus is a sign, not the separator
```

Both bounds of a range are included and the first must not be greater than the
second. A range picks the minimum plus a whole number, so a `double` range
`"0.5-2.5"` gives `0.5`, `1.5` or `2.5`. Quoted numbers use the invariant culture.

Written form: a fixed whole value as a bare integer (`amount = 5`), a fixed fraction
as a bare float (`amount = 2.5`), a range as the quoted text (`amount = "5-10"`).

Errors:

| Value | Result |
| --- | --- |
| `amount = "not-a-range"` | `'not-a-range' is not a valid Int32 value or range.` |
| `amount = "10-5"` | `'10-5' is not a valid Int32 value or range.` |
| `amount = true` | `Expected a number or a range for Int32.` |
| `small = 300` on a `RangeValueSpec<byte>` | `Arithmetic operation resulted in an overflow.` |
| `amount = 1.7` on a `RangeValueSpec<int>` | no error: a bare float is cut to `1` |

Used by: `amount` in loot templates. See
[Fields that resolve to a fresh number](templates.md#fields-that-resolve-to-a-fresh-number).

## DiceSpec

A `DiceSpec` is a number field rolled with dice notation, or a constant. Mobile
templates use it for stats, skills, damage, resistances, karma, fame and gold.

Accepted forms:

```toml
armor = 20             # bare integer: constant
karma = -2500          # negative constants are allowed
karma = "-2500"        # the same, quoted
strength = "1d25+95"   # one 25-sided die plus 95: 96 to 120
damage = "3d4+2"       # three 4-sided dice plus 2: 5 to 14
hits = "4d6k3"         # four 6-sided dice, keep the highest three
mana = "(2d6+1)*10"    # parentheses, *, /
```

`NdM` rolls N dice of M sides; `+`, `-`, `*`, `/`, parentheses and `k` (keep the
highest) combine them. A uniform range from a to b is one die,
`1d(b-a+1)+(a-1)`. **`"96-120"` is not a range**: it is 96 minus 120, a constant -24.

Written form: a constant as a bare integer, an expression as the quoted text it was
read from.

Errors:

| Value | Result |
| --- | --- |
| `strength = "2d"` | `'2d' is not a number or a dice expression.` |
| `strength = true` | `Expected a number or a dice expression.` |

Used by: `MobileTemplate`. See [The template shapes](templates.md#the-template-shapes).

## Enums

Every enum is written as its snake_case name and read back from it, through
`EnumTomlConverterFactory`, which `TomlUtils` always includes in its default
options. The rules live in `EnumNameUtils`, which `EnumValueSpec` shares.

```toml
minimum_account_type = "game_master"   # AccountType.GameMaster
map = "ter_mur"                        # MapType.TerMur
music = "mountn_a"                     # MusicType.Mountn_a
```

- **Reading** ignores case and underscores: `"game_master"`, `"GameMaster"` and
  `"gamemaster"` are the same, and `"termur"` reads `MapType.TerMur`.
- **Writing** uses the lowercase snake_case name, which always reads back.
- **Numbers** are never accepted, bare or in a string: write the name. Data keyed by
  the client's numbers, such as `skills.toml`, names the member instead
  (`id = "alchemy"` is `SkillType.Alchemy`, value 0).

**Flags.** A `[Flags]` enum writes a value that has its own name as that name, and
any other combination as names joined by `|`; reading combines the names:

```toml
mode = "standalone"              # ServerMode.Login | ServerMode.Game has its own name
mode = "login|game"              # reads the same value
flags = "impassable|surface"     # TileFlagType.Impassable | TileFlagType.Surface
```

Spaces around the names are ignored. A combination is split into the largest named
parts, so `DirectionType.SouthEast | DirectionType.Running` is written as
`"running|south_east"`, not as single bits. Zero is written as the name of the zero
member when there is one (`"none"`), and as an empty string otherwise; an empty string
reads as zero for a flags enum only. A plain enum rejects `|`.

Errors:

| Value | Result |
| --- | --- |
| `minimum_account_type = "gm"` | `'gm' is not a AccountType; use one of regular, game_master, administrator.` |
| `minimum_account_type = "1"` | the same message, naming `1` |
| `minimum_account_type = 1` | `Expected a AccountType name as a string, such as "regular".` |
| `minimum_account_type = true` | `Expected a AccountType name as a string, such as "regular".` |

A value is still checked by the code that loads it: `mode = "none"` reads, then the
configuration rejects `ServerMode.None`.

Used by: `mode` and `realm_directory.minimum_account_type` in `moongate.toml`,
`visibility` in item templates, and the enum keys of the [shard data files](data-files.md),
such as `map`, `season`, `music` and `skill`.

## Write a converter

For a type of your own, subclass `TomlConverter<T>` for one closed type, or
`TomlConverterFactory` when the type is generic. Then register an instance with
`TomlUtils.AddTomlConverter` at startup, usually from a plugin's `Register`, or name
the converter in a `[TomlConverter(typeof(...))]` attribute on the property or type.

A few rules keep converters consistent:

- In `Read`, check `reader.TokenType` and throw `reader.CreateException(...)` for a
  value you do not accept; the exception gets the line and column. Name the
  offending text in the message.
- In `Write`, throw `TomlException` for a value that has no TOML form.
- Format and parse numbers with `CultureInfo.InvariantCulture`.
- Write the form a person would type, and make `Read` accept what `Write` produces.

### Worked example: Serial

`SerialTomlConverter` reads a bare integer, which TOML parses natively, or the same
value quoted, and always writes a bare integer:

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

        return new((uint)reader.GetInt64());
    }

    public override void Write(TomlWriter writer, Serial value)
    {
        writer.WriteIntegerValue(value.Value);
    }
}
```

### Worked example: Point2D and Point3D

A type with a text form of its own can reuse it. `Point2DTomlConverter` accepts only a
string, parses it with `Point2D.TryParse` and writes `ToString`, both with the
invariant culture:

```csharp
public sealed class Point2DTomlConverter : TomlConverter<Point2D>
{
    public override Point2D Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Expected a \"(x, y)\" string for a Point2D.");
        }

        var text = reader.GetString();

        if (!Point2D.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            throw reader.CreateException($"'{text}' is not a valid Point2D, expected \"(x, y)\".");
        }

        return parsed;
    }

    public override void Write(TomlWriter writer, Point2D value)
    {
        writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));
    }
}
```

`Point3DTomlConverter` is the same with three coordinates.

### Generic types and tables

`EnumValueSpecTomlConverterFactory` shows the factory shape: `CanConvert` claims every
closed `EnumValueSpec<>`, and `CreateConverter` builds the matching
`EnumValueSpecTomlConverter<TEnum>` by reflection, so one registration covers every
enum.

`RegionAreaContentTomlConverter` shows a converter that reads both a string and an
inline table: it walks the table's property names until `EndTable`, and writes with
`WriteStartInlineTable`, `WritePropertyName` and `WriteEndInlineTable`.

## See also

- [Shard data files](data-files.md): the files under `data/` and their fields.
- [Loading TOML templates](templates.md): item and loot templates and the loader
  contract.
- [Server configuration](server-configuration.md): `moongate.toml`.
