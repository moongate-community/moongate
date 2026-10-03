# Loading TOML templates

Shard content that a designer authors by hand, such as item and mobile definitions,
is a set of TOML files under `templates/` in the server root, read once when the
shard starts. The distribution's templates are copied there by
[`mgctl`](mgctl.md). This page covers the loader contract in `Moongate.Server.Ultima` and
the TOML value types in `Moongate.Core` that make templates pleasant to write by
hand; [TOML value types](toml-types.md) is the reference for their text forms. It
assumes [writing a plugin](plugins.md), since a loader is registered from `Register`
the same way a service or a metric provider is.

## What exists today

The loader contract, `DataLoaderService`, `EnumValueSpec<TEnum>`, `RangeValueSpec<T>`
and the converter registry are in place and tested. The `ItemTemplate`,
`MobileTemplate` and `LootTemplate` data shapes exist, and [a converter](uox3-migration.md) produces them
from UOX3 data. **Item and mobile templates are loaded** (see
[Item templates at runtime](#item-templates-at-runtime) and
[Mobile templates at runtime](#mobile-templates-at-runtime)), and so are loot tables (see
[Loot tables at runtime](#loot-tables-at-runtime)).
The same contract already loads the files under `data/`, such as maps, races and
regions: see [Shard data files](data-files.md) for working loaders.
See [Implementation status](implementation-status.md).

## Item templates at runtime

`ItemTemplatesLoader` reads every `*.toml` under `templates/items/`, subfolders
included, when the game server starts, and resolves `base_id` once:

- a field that can be left unset (`name`, `layer`, `weight`, `amount`, `stackable`, …)
  and is left unset takes the parent's value, up the chain;
- `item_id = 0` takes the parent's graphic;
- `hue` inherits when unset; without a value anywhere in the chain, it uses 0;
- `rarity` always has a value, so the template's own is kept;
- `tags`, when set, replace the parent's; they are not merged.

The server stops when an id is empty or used twice (the message names both files),
a `base_id` names no template, `base_id` loops back (`a` → `b` → `a`), or a resolved
template fails `ItemTemplate.Validate()`.

`IItemTemplateService` serves the resolved templates (`TryGet`, `Get`, `Count`; ids
match case). `IItemFactoryService` makes items from them:

```csharp
var coin = factory.Create("0x0eed_gold_coin", amount: 250);
coin.PutInContainer(backpack.Id, layout.RandomGridPosition(backpack.ItemId)); // or PlaceOnGround / Equip
await factory.SaveAsync(coin);          // the database gives it its serial
```

`Create` builds the `ItemEntity` in memory, with no serial and no location. Random
template values (`hue`, `amount`, `rarity`) are picked once, there, and stored with
the item; nothing else is copied from the template, so `name`, `movable` and
`visibility` stay null and the template's values apply. An amount above 1 on an item
that does not stack (`stackable`, else the tiledata `Generic` flag) throws. Saving
refuses an item with no location and gives a new item a serial from
`world.items_id_seq`, inside `Serial.MinItem..MaxItem`. `SaveAsync(items)` saves a
list in one transaction, in order (a container before its contents), and
`SaveAsync(transaction, item)` saves inside a transaction the caller opened.

## Mobile templates at runtime

`MobileTemplatesLoader` reads every `*.toml` under `templates/mobiles/`, subfolders
included, after the item templates, and resolves `base_id` once, as UOX3's `GET` does
(the child inherits everything and overrides what it sets):

- a field left unset takes the parent's value, up the chain;
- `skills`, `resistances` and `sounds` are inherited key by key: the child's entries
  override, the parent's other entries stay;
- `tags` merge: the child's keys add to and override the parent's;
- `equipment` and `loot`, when set, replace the parent's;
- everything inherited is copied, never shared between templates.

The server stops when an id is empty or used twice, a `base_id` names no template or
loops back, a template fails `MobileTemplate.Validate()`, a `name_list` names no list of
`data/names.toml` (`{gender}` needs the `male` and `female` lists), an equipment item is
not an item template, or a `loot` id is not a loot table. `IMobileTemplateService` serves the resolved templates (`TryGet`, `Get`,
`Count`).

`IMobileFactoryService` makes NPCs from them:

```csharp
var spawned = await mobiles.SpawnAsync("guard", MapType.Felucca, new Point3D(1602, 1591, 20));
// spawned.Mobile is saved with its serial; spawned.Equipment is what it wears.
```

`Create(templateId)` builds the `MobileEntity` in memory and rolls every random value
once: gender (`random` is 50/50, unset is male), body (the template's, else the race body for
the gender from `races.toml`), name (the template's, else one of `name_list`, where
`{gender}` picks the `male` or `female` list), skin, hair and beard (from the template,
else the race; `hair = []` is bald; females get no beard), stats (unset is 10), hits,
mana and stamina (which default to strength, intelligence and dexterity), armor, resistances, fame,
karma and skills (template points × 10, the tenths the mobile stores). `title` and
`notoriety` stay null: the template's apply.

`SpawnAsync(templateId, map, location)`:

1. rejects a location outside the map, before anything else, and a template that resolves
   to no body and no race;
2. creates the mobile and places it;
3. publishes `MobileBeforeSpawnEvent`: a handler may change the mobile, even move it; the
   place is checked against the map again afterwards;
4. in one transaction, saves the mobile (its serial comes from the mobile range), then:
   - a backpack (`ultima.items.backpack_template`) worn on the `Backpack` layer, for every NPC;
   - each equipment entry through `IItemFactoryService`: an entry with a `gender` is
     skipped for the other gender; the item is worn on its layer (the template's, else
     tiledata's for a wearable graphic); an item with no layer, or whose layer is taken,
     goes into the backpack, as UOX3 does;
   - the rolled `gold`, as `ultima.items.gold_template` piles of at most 65535, into the backpack;
   - one roll of each `loot` table (a table listed twice is rolled twice), into the
     backpack;
   - everything in the backpack lands at a random spot inside its `containers.toml` bounds;
5. after the commit, publishes `MobileMovedToWorldEvent` (with the place the mobile was
   saved at) and then `MobileAfterSpawnEvent`, without cancellation: the mobile is saved
   by then. A failure before the commit leaves the in-memory mobile without a serial.

The event bus logs a handler's exception and goes on, so a handler cannot stop or undo a
spawn.

`SaveAsync(mobile)` saves a mobile that already has its serial.

## Loot tables at runtime

`LootTemplatesLoader` reads every `*.toml` under `templates/loots/`, subfolders included,
before the mobile templates. The server stops when an id is empty or used twice, an entry
sets both `item_id` and `loot_template_id`, an `item_id` is not an item template, a
`loot_template_id` names no table, a `weight` is below 1, an `amount` can roll outside 1
to 65535, or nested tables loop. A table with no entries is kept, logged as a warning, and gives
nothing: two shipped UOX3 tables (`randomwands`, `random_useless_junk`) are empty in
UOX3's own data.

`ILootService.Roll(tableId)` picks **one** entry, in proportion to `weight`, and returns
what it gives, built through `IItemFactoryService` with no serial and no location: nothing
for an entry with neither id (UOX3's `blank`), one pile for a stackable item, that many
separate items otherwise, and, for a nested table, `amount` rolls of it: `LOOTLIST=randomgems,2` rolls `randomgems` twice. (UOX3 itself
looks the nested entry up as an item list and spawns nothing; Moongate does what the data
plainly means.)

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

Parsing member names ignores case and underscores; writing uses lowercase
snake_case, so `FromValue(ItemRarityType.Epic).ToString()` is `"epic"`, matching how
a designer types it, and what is written always reads back. See
[EnumValueSpec](toml-types.md#enumvaluespec) for every accepted form and error. A
field declares this by its type, nothing else:

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

As text, a bare number (`amount = 5`) is fixed; a quoted `min-max` (`amount = "5-10"`)
picks a fresh value in that inclusive range on every `Resolve()`. A quoted bare
number (`amount = "5"`) is accepted too. Writing a fixed value emits a bare number;
writing a range emits the quoted form. See
[RangeValueSpec](toml-types.md#rangevaluespec) for every accepted form and error.

```csharp
public RangeValueSpec<int> Amount { get; set; } = RangeValueSpec<int>.FromValue(1);
```

Hues have their own type, `HueSpec`, with the same fixed-or-range text form and hex
values such as `"0x03EA-0x0422"`; `ItemTemplate.Hue` uses it. See
[HueSpec](toml-types.md#huespec).

## Registering a TOML converter

`EnumValueSpec<TEnum>`, `RangeValueSpec<T>`, `HueSpec`, `Serial` and the point types
read and write through converters that `MoongateUltimaPlugin` registers once with
`TomlUtils.AddTomlConverter`; `Visibility` uses a converter named by an attribute.
A template needs nothing more than the field type. For the accepted and written
forms of every type, the errors, and how to write and register a converter of your
own, see [TOML value types](toml-types.md).

## The template shapes

`ItemTemplate`, in `Moongate.Server.Ultima`, is a plain data shape:

| Field | Purpose |
| --- | --- |
| `Id` | The stable name a loot table, a spawn or `additem` names this template by |
| `BaseId` | Another template's `Id` to inherit unset fields from; the loader resolves the chain |
| `ItemId` | The base client graphic; runtime physical properties come from `ITileDataService` unless overridden |
| `Name`, `Comment` | A display name override, and a designer note nobody reads at runtime |
| `Rarity` | `EnumValueSpec<ItemRarityType>` |
| `ScriptId` | The global Lua table, defined by `scripts/items/<script_id>.lua`, whose functions (`on_use`, `on_equip`, `on_unequip`, `on_pickup`, `on_drop`, `on_create`) handle what happens to the item; a lower-case Lua identifier, empty for none. See [Item scripts](scripting.md#item-scripts) |
| `Movable` | Unset uses tiledata: movable unless the tiledata weight is 255, the client's "cannot be lifted" |
| `Weight` | Stones to two decimals (`weight = 0.02` for a coin); unset uses the whole-stone tiledata weight |
| `Amount` | `RangeValueSpec<int>`: the stack size of a new item, fixed or `"10-20"`; unset is 1 |
| `Stackable` | Unset uses the tiledata `Generic` flag |
| `Layer` | A `LayerType` name such as `one_handed`; unset uses the tiledata layer |
| `TwoHandedWeapon` | `two_handed_weapon = true` on a weapon held in both hands (bows, polearms, staves), as POL's `TwoHanded`: worn on `two_handed`, it leaves no hand free. Anything else on `two_handed` (shields, torches) goes in the other hand, with a one-handed weapon. Tiledata cannot tell them apart: it marks shields as weapons and bows as one-handed |
| `BuyPrice`, `SellPrice` | What vendors sell it for and pay for it; unset means vendors do not trade it |
| `Decays`, `DecayMinutes` | Whether the item decays on the ground, and after how many minutes; unset decays when movable, after 60 minutes, as ModernUO. An item that cannot be picked up decays only when its template has both `decays = true` and `decay_minutes`, as the treasure chests. An item decays when it lies on the ground, is movable and its template is visible to players. The countdown (`DecayAt`, saved with the item, so downtime counts) starts when it lands on the ground, again when a lifted item bounces back there, and stops when it is picked up, moved into a container or worn; the rest of a split stack keeps the stack's time. A check every 5 seconds deletes the due items, a container with its contents, whether or not a player is near |
| `LootType` | `regular`, `newbied`, `blessed` or `cursed`: what happens when the owner dies; unset is `regular` |
| `Tags` | Free script values in an `[item.tags]` table; a child's explicit tags replace the entire base map |
| `Visibility` | The lowest account type that sees the item: `regular`, `game_master` or `administrator`, as `realm_directory.minimum_account_type`. Unset by default, so a template inherits it through `BaseId`; an item with none anywhere is visible to everyone. `IsVisibleTo(accountType)` answers for one viewer |
| `Hue` | `HueSpec`, `0` meaning the art's native coloring; a quoted `"min-max"` range picks one per spawn |
| `MaxItems`, `MaxWeight` | Nullable; set only on a container template |

Fields that the client's `tiledata.mul` also carries (weight, stackability, layer,
movability) are overrides: unset means tiledata, as in POL and ModernUO. The extensions
in `ItemTemplateExtensions` give the value a new item gets, such as
`template.EffectiveWeight(tileDataService)`, and `Validate()` rejects a negative weight or
price, a weight with more than two decimals, an amount below 1, a decay time below one
minute and an empty tag key.

Spawners, for example, are for staff only, and their children inherit it:

```toml
[[item]]
id = "base_spawner"
base_id = "base_item"
item_id = 7956
visibility = "game_master"

[[item]]
id = "orcspawn"
base_id = "base_spawner"
item_id = 7956
name = "Orc Spawner"
```

`MobileTemplate` defines a mobile, creature or human NPC, one `[[mobile]]` entry
under `templates/mobiles/`. Every field but `Id` may be unset: it is inherited through
`BaseId`, and with none anywhere the default below applies. Numbers that vary per
mobile are dice ([`DiceSpec`](toml-types.md#dicespec)): `strength = "1d25+95"` rolls 96
to 120; a constant is a bare integer.

| Field | Purpose |
| --- | --- |
| `Id`, `BaseId`, `Comment` | As in `ItemTemplate` |
| `Name` | A fixed name such as `an orc`; unset draws one from `NameList` |
| `NameList` | A list id in `data/names.toml`; `{gender}` becomes `male` or `female`, the gender the mobile gets |
| `Title` | Shown after the name, such as `the guard` |
| `Body` | The body the client draws; unset uses the `Race` body for the gender |
| `Gender` | `male`, `female` or `random` (50/50 for every mobile); unset is `male` |
| `Race` | `human`, `elf` or `gargoyle`: skin, hair and beard come from `races.toml` unless set here; unset for a creature |
| `SkinHue`, `HairHue`, `BeardHue` | `HueSpec`; unset uses the race hues |
| `Hair`, `Beard` | Item ids, one picked; unset uses the race styles for the gender, and females get no beard |
| `Strength`, `Dexterity`, `Intelligence` | Dice; unset is 10 |
| `Hits`, `Mana`, `Stamina` | Dice; unset is the strength, the intelligence and the dexterity |
| `Damage`, `Armor` | Dice for an unarmed hit and the natural armour; unset is `1d4` and 0 |
| `Resistances` | `[mobile.resistances]` with `physical`, `fire`, `cold`, `poison`, `energy`, dice in percent; unset is 0 |
| `Skills` | `[mobile.skills]`, skill names such as `resisting_spells` or `tactics`, dice in whole points 0 to 120 |
| `Notoriety` | `innocent`, `ally`, `attackable`, `criminal`, `enemy`, `murderer` or `invulnerable`, the name colour; unset is `innocent` |
| `Karma`, `Fame` | Dice; karma may be negative |
| `Equipment` | `[[mobile.equipment]]` entries: `items` (item template ids, one picked), `hue`, and `gender` to equip only one gender |
| `Loot`, `Gold` | Loot template ids and gold dice rolled into the backpack at spawn; no corpse system yet |
| `Sounds` | `[mobile.sounds]` with `start_attack`, `idle`, `attack`, `hurt`, `death`; a mobile script plays them by kind with `npc.play_sound(serial, "idle")` |
| `ScriptId` | The global Lua table, defined by `scripts/mobiles/<script_id>.lua`, whose `on_think`, `on_speech`, `on_spawn` and `on_mobile_in_range` handle the NPC; a lower-case Lua identifier. See [Mobile scripts](scripting.md#mobile-scripts) |
| `Visibility` | As in `ItemTemplate` |
| `Movement` | `land`, `water` (a dolphin: it spawns and swims on the water only) or `both` (a walrus: it walks and swims, and spawns on land else on the water); unset is `land` |
| `Tags` | Free script values; child keys add to and override parent keys |

`Resistances`, `Sounds`, `Skills` and `Tags` are inherited key by key, so a base such as
`base_orc` sets the five sounds once and every orc keeps them; every other field a child
sets replaces the base's. `Validate()` rejects dice that can roll below 0 (karma apart),
an unknown skill, a skill above 120, a resistance above 100, a negative sound, an
equipment entry with no item or an empty item id, and an empty tag key.

```toml
[[mobile]]
id = "base_orc"
body = 0x11
name = "an orc"
notoriety = "murderer"

[mobile.sounds]
start_attack = 0x1B0
idle = 0x1B1
attack = 0x1B2
hurt = 0x1B3
death = 0x1B4

[[mobile]]
id = "orc"
base_id = "base_orc"
strength = "1d25+95"
karma = -2500
loot = ["orc_loot"]

[mobile.skills]
tactics = "1d26+54"

[[mobile]]
id = "guard"
gender = "random"
race = "human"
name_list = "{gender}"
title = "the guard"
notoriety = "invulnerable"
script_id = "wander"

[[mobile.equipment]]
items = ["leather_skirt", "leather_shorts"]
gender = "female"
```

The shipped `templates/mobiles/` holds UOX3's NPCs, converted by
[`mgctl convert uox`](uox3-migration.md#mobiles-and-name-lists). They are loaded at game and
standalone startup (`IMobileTemplateService`).

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

## NPC lists and spawns

`templates/npc_lists/` holds lists of mobile templates a spawn picks from, converted from UOX3's
`[NPCLIST name]` blocks. An entry names a mobile template or another list, and is picked in
proportion to its `weight` (1 by default); an entry naming a list then picks from that list:

```toml
[[npc_list]]
id = "jungle"
entries = [{ mobile_id = "gorilla", weight = 20 }, { npc_list_id = "all_trolls", weight = 7 }]
```

`templates/spawns/<map>/` holds the spawn regions, converted from UOX3's `[REGIONSPAWN n]` blocks,
plus ModernUO's spawners (`modernuo_*.toml` and `trammel/town_new_haven.toml`); the folder is the
map, and a `map` key in a region is ignored. A spawn picks from one pool, as UOX3: its `mobile_ids` (weight 1 each) and
the entries of its `npc_list_ids` with their weights:

```toml
[[spawn]]
id = "felucca_0"                      # unique
name = "The Hammer And Anvil"
mobile_ids = ["weaponsmith"]          # mobile templates, picked at random with the lists' entries
npc_list_ids = []                     # npc lists
max = 1                               # NPCs alive at once
min_minutes = 480                     # a new one every min_minutes to max_minutes
max_minutes = 600
call = 1                              # NPCs that come at a time
areas = [{ x1 = 1422, y1 = 1547, x2 = 1426, y2 = 1550 }]   # both corners included
exclude = []                          # parts of the areas where nothing spawns
only_outside = false                  # true: never under a roof
# pref_z = 18                         # how high above the ground a spot may be
# z = 36                              # the highest a spot may be, instead of ground + pref_z
```

Both load at startup, after the mobile templates, and a mistake in them stops the server. How the
regions spawn at runtime, water mobiles included, is in [NPC spawns](spawns.md).

## Decorations

`templates/decorations/` holds the world decoration the client's map files do not: doors, signs,
lights, furniture, teleporters and the like, about 42,600 placements in 115 files. It was
converted once from ModernUO's `Data/Decoration`, plus ServUO's New Haven (`trammel/newhaven.toml`,
`havenisland.toml`, `havenmine.toml`, which ModernUO lacks) and the shop and world signs of
ModernUO's `signs.cfg` (`signs.toml`, written by
[`mgctl convert modernuo-signs`](uox3-migration.md#signs-of-modernuo)) and the world and dungeon
teleporters of its `teleporters.json` (`teleporters.toml`, written by
[`mgctl convert modernuo-teleporters`](uox3-migration.md#teleporters-of-modernuo)), one TOML file per source file, in one folder
per map: `britannia/` (Trammel and Felucca), `trammel/`, `felucca/`, `ilshenar/`, `malas/`,
`tokuno/`, `termur/`, and the special sets `_ruined_magincia_tram/`, `_ruined_magincia_fel/` and
`_bounty_boards/`. A folder whose name starts with `_` is not loaded: rename it without the `_`
to place its decoration. Files starting with `_` inside a loaded folder (the dungeons, such as
`britannia/_covetous.toml`) are loaded.

```toml
[[decoration]]
comment = "metal door"
type = "MetalDoor"                # the kind, kept as ModernUO names it
item_id = 0x0675
props = { facing = "west_cw" }    # the kind's settings; facing is a DoorFacingType
locations = [[1411, 1621, 30], [1411, 1622, 30]]
```

A block without `item_id` is an addon built from several graphics. `extras`, when present, gives a
setting per location in the order of `locations`.

[`.decorate`](commands/decorate.md) places them with the templates of
`templates/items/decorations.toml`: `decoration`, fixed and never decaying, for most kinds;
`decoration_door`, the same with `script_id = "door"`, for the kinds whose name contains `Door`
or `Gate`; `decoration_light`, with `script_id = "light"`, for ModernUO's light kinds
(candles, candelabras, lanterns, lamp posts, sconces, torches, braziers); and
`decoration_teleporter`, with `script_id = "teleporter"` and `visibility = "game_master"`, for
the kind `Teleporter`. Each item takes the
block's graphic, `hue` and `name`; its other settings stay in the item's props, with
`decoration_type` = the kind for a door or a light. A light also gets its `light` shape (the
block's or the kind's) and `protected` unless the block says `unprotected`; the graphic already
says whether it is lit. A door block with `locked = true` in its props places doors that only
staff open. An item with a `label_number` prop, such as a `LocalizedSign`, shows that text of the
client as its name, unless the item has a name of its own. A teleporter's `point_dest = [x, y, z]` becomes the props `teleport.x`,
`teleport.y` and `teleport.z`, and its `map_dest` the prop `teleport.map`, a `MapType` number.
A `KeywordTeleporter` takes the template `decoration_keyword_teleporter`, with
`script_id = "keyword_teleport"`, and keeps its `substring`, `keyword`, `range` and `delay` as props.
A `PublicMoongate` takes the template `decoration_public_moongate`, with
`script_id = "public_moongate"`; `.decorate` also places one on every destination of
[`moongates.toml`](data-files/moongates.md).
Spawners, mark containers, addons and every other kind whose name ends in
`Teleporter`, those that ask for a skill, a quest or a double click (`SkillTeleporter`,
`InteractionTeleporter`, ...), are not placed yet. The doors of the towns are in no file:
`.decorate` reads them from the map's door frames.

