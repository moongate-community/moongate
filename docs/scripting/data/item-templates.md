# Item templates

Item templates are the YAML source of truth for every item id `item.create`,
`item.create_by_tag` and `item.create_by_category` can spawn. They ship under
`src/Moongate.Server/Assets/Templates/Items/` (recursively, including the
`base/` and `gm/` subfolders — the folder layout is purely organizational,
`ItemTemplatesLoader` scans it depth-first).

| Concern | Type |
|---|---|
| DTO | `ItemTemplate` (`Moongate.UO.Data.Items`) |
| Nested specs | `EquipSpec`, `WeaponSpec`, `ContainerSpec`, `BookSpec`, `ItemParam` (same namespace) |
| Loader | `Moongate.Server.Loaders.ItemTemplatesLoader` |
| Deserializer | `Moongate.Server.Services.Items.ItemTemplateYamlDeserializer` |
| Validator | `Moongate.Server.Services.Items.ItemTemplateValidator` |

This page documents every key the DTO binds. For the runtime behaviour (how
templates become spawned items, containers and equipment) see the
[Items guide](../guides/items.md) and the [`item` module reference](../reference/item.md).

> [!NOTE]
> Item templates are **flat** — there is no `base_item` inheritance key on the
> DTO. Any shared attributes between items in `base/` and the templates that
> resemble them were factored out at authoring time, not resolved at load
> time (contrast this with [mobile templates](mobile-templates.md), which do
> have a real `BaseMobile` merge step).

## File shape and identity

Each YAML file is a top-level **list** of item template objects (`- Id: ...`
entries), not a single mapping. The loader concatenates every `*.yaml` file
under the items directory, in case-insensitive path order, into one flat set
of templates.

- **`Id` must be present and unique** (case-insensitive) across *all* files.
  A duplicate `Id` — even across two different files — is a hard load error.
- `Id`, `Name` and `Category` must all be non-blank; a missing or
  whitespace-only value is a load error.
- Numeric fields (`ItemId`, `Hue`, `GoldValue`, `Weight`, and every numeric
  field inside `Equip`/`Weapon`/`Container`) must be non-negative, and
  `Weight` must additionally be finite (no `NaN`/`Infinity`).
- A key that is non-nullable on the DTO (for example `ItemId`, `Hue`,
  `IsMovable`, `Rarity`) but written as an explicit YAML null (`~`, empty, or
  `null`) is a **shape error**, even though C# would otherwise default it —
  the deserializer rejects it before it ever reaches the DTO. Omitting the
  key entirely is fine and just takes the default.
- `Tags` must not be an explicit YAML null (omitting it defaults to an empty
  list, which is fine); every element in `Tags`, `FlippableItemIds`,
  `LootTables` and every value in `Params` must be non-null.
- `Rarity` must be a defined `ItemRarityType` member, and `Equip.Layer` (when
  `Equip` is present) must be a defined `LayerType` member — see
  [`layer_type`](../reference/enums.md#layer_type) for the full list.

## Top-level keys

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Id` | `string` | required, unique | Template identifier used everywhere else (`item.create`, loot entries' `ItemTemplateId`, mobile `Equipment[].Item`). |
| `Name` | `string` | required | Display name, copied onto the spawned item's `Name`. |
| `Category` | `string` | required | Free-form grouping; matched (case-insensitive) by `item.create_by_category` and `IItemFactoryService.CreateByCategory`. |
| `Description` | `string` | optional, default `""` | Flavor text, copied onto the spawned item's `Description`. |
| `ItemId` | `int` | optional, default `0` | The UO art/tile id. Must be non-negative. |
| `Hue` | `int` | optional, default `0` | Default hue index, used unless the caller passes an explicit hue (e.g. `item.create`'s hue argument). |
| `GoldValue` | `int` | optional, default `0` | Declared gold value. **Reserved** — not read by `ItemFactoryService` or copied onto the spawned item today. |
| `Weight` | `double` | optional, default `0.0` | Item weight. Must be finite and non-negative. |
| `ScriptId` | `string` | optional, default `""` | Free-form script identifier, copied onto the spawned item's `ScriptId`. **Reserved** — no loader in this codebase currently dispatches on it. |
| `IsMovable` | `bool` | optional, default `false` | Declared and shape-validated but **not yet read** by `ItemFactoryService`. |
| `Rarity` | `ItemRarityType` enum | optional, default `Common` | One of `Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`, `Artifact`. Must be a defined member. Copied onto the spawned item's `Rarity`. |
| `Tags` | `List<string>` | optional, default `[]` | Free-form labels. Drives `item.create_by_tag` / `CreateByTag`, and is the pool loot entries' `ItemTag` matches against. Cannot be an explicit YAML null. |
| `Stackable` | `bool?` | optional, default `null` | Declared on the DTO. **Reserved** — no consumer in this codebase. |
| `Dyeable` | `bool?` | optional, default `null` | Declared on the DTO. **Reserved** — no consumer in this codebase. |
| `Visibility` | `string?` | optional, default `null` | Free-form string (shipped value seen: `GameMaster`). **Reserved** — distinct from the spawned entity's own `Visibility` (an `AccountLevelType`), which this key is not wired to. |
| `LootType` | `string?` | optional, default `null` | Free-form string (shipped value seen: `Blessed`). **Reserved** — no consumer in this codebase. |
| `FlippableItemIds` | `List<int>?` | optional, default `null` | Alternate `ItemId` graphics the item cycles through. Consumed by `item.flip` / `IItemService.Flip`. Each element must be non-negative and non-null. |
| `LootTables` | `List<string>?` | optional, default `null` | Loot template ids associated with this item. Declared and shape-validated (elements non-null) but **not auto-rolled** by any loader — roll them explicitly with [`loot.roll`](../reference/loot.md#lootroll). |
| `Params` | `Dictionary<string, ItemParam>?` | optional, default `null` | Typed script parameters keyed by name. Values must be non-null. **Reserved** — no script-parameter resolver currently reads this. |
| `Equip` | `EquipSpec?` | optional, default `null` | Present ⇒ the item is wearable. See [EquipSpec](#equipspec). |
| `Weapon` | `WeaponSpec?` | optional, default `null` | Present ⇒ the item is a weapon. See [WeaponSpec](#weaponspec). |
| `Container` | `ContainerSpec?` | optional, default `null` | Present ⇒ the item is a container. See [ContainerSpec](#containerspec). |
| `Book` | `BookSpec?` | optional, default `null` | Present ⇒ the item is a book. See [BookSpec](#bookspec). |

`Equip`, `Weapon`, `Container` and `Book` are independent — the DTO does not
enforce that they are mutually exclusive — but in shipped data each item
uses at most one of them.

## EquipSpec

Nested under `Equip:`.

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Layer` | `LayerType` enum | optional, default `None` (0) | The [paperdoll layer](../reference/enums.md#layer_type) this item equips to. Must be a defined member. Consumed by `CharacterService` to auto-equip starting items. |
| `HitPoints` | `int?` | optional, default `null` | Durability. Must be non-negative when set. |
| `StrengthReq` | `int?` | optional, default `null` | Strength required to equip. Must be non-negative. |
| `DexterityReq` | `int?` | optional, default `null` | Dexterity required to equip. Must be non-negative. |
| `IntelligenceReq` | `int?` | optional, default `null` | Intelligence required to equip. Must be non-negative. |

`HitPoints`/`StrengthReq`/`DexterityReq`/`IntelligenceReq` are declared and
validated but, aside from `Layer`, not yet enforced anywhere at equip time.

## WeaponSpec

Nested under `Weapon:`. All numeric fields are non-nullable `int` on the DTO
(default `0`) and must be non-negative; `WeaponSkill` and the ammo fields are
nullable.

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `LowDamage` | `int` | optional, default `0` | Minimum base damage. |
| `HighDamage` | `int` | optional, default `0` | Maximum base damage. |
| `Speed` | `int` | optional, default `0` | Swing speed rating. |
| `BaseRange` | `int` | optional, default `0` | Minimum effective range. |
| `MaxRange` | `int` | optional, default `0` | Maximum effective range. |
| `HitSound` | `int` | optional, default `0` | Sound id played on a hit. |
| `MissSound` | `int` | optional, default `0` | Sound id played on a miss. |
| `WeaponSkill` | `string?` | optional, default `null` | Name of the skill that governs this weapon (e.g. `Swords`). Free-form string, not enum-validated. |
| `Ammo` | `int?` | optional, default `null` | Ammo item reference (ranged weapons). Must be non-negative. |
| `AmmoFx` | `int?` | optional, default `null` | Ammo flight effect id. Must be non-negative. |

`WeaponSpec` is currently declarative: it is shape- and range-validated at
load time, but no combat service in this codebase reads it yet.

## ContainerSpec

Nested under `Container:`. All fields are nullable; every numeric field must
be non-negative when set.

**Declaring `Container:` at all is what makes an item a container** — it is the
question the server asks before opening anything, reached from the item's
`TemplateId`. The client's own opinion does not enter into it: plenty of
graphics are containers in `tiledata.mul` that no shard opens (key rings,
potion kegs, spellbooks), and ModernUO ignores that flag for exactly the same
reason — there, container-ness is which C# class the item is.

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `WeightMax` | `int?` | optional, default `null` | Maximum total weight the container can hold. |
| `MaxItems` | `int?` | optional, default `null` | Maximum item count. |
| `GumpId` | `int?` | optional, default `null` | The window the client opens on double-click. Leave it out and the gump table is asked for one matching `ItemId`; failing that it is gump `60`, the plain bag. Omitting it is the right call whenever the table already knows the graphic. |
| `ContainerLayoutId` | `string?` | optional, default `null` | Identifier for a client-side container layout/background. |
| `Contents` | `List<string>?` | optional, default `null` | Item template ids to pre-populate the container with. Elements must be non-null. Declared but not yet consumed by a spawn-time populator. |
| `IsQuiver` | `bool?` | optional, default `null` | Marks the container as a quiver. |
| `WeightReduction` | `int?` | optional, default `null` | Quiver weight-reduction percentage. |
| `QuiverDamageIncrease` | `int?` | optional, default `null` | Quiver damage-increase bonus. |
| `LowerAmmoCost` | `int?` | optional, default `null` | Quiver lower-ammo-cost bonus. |
| `DefenseChanceIncrease` | `int?` | optional, default `null` | Quiver defense-chance-increase bonus. |

## BookSpec

Nested under `Book:`.

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `BookId` | `string` | optional, default `""` | Identifier of the book's content (resolved elsewhere to actual page text, e.g. `templates/books/<BookId>.txt`). |

## ItemParam

Values of the `Params` dictionary (`Params: { <name>: { Type, Value } }`).

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Type` | `string` | optional, default `""` | Declared parameter type (free-form, e.g. `string`). |
| `Value` | `string` | optional, default `""` | Parameter value, always stored as a string. |

## Full annotated example

`halberd`, from
`src/Moongate.Server/Assets/Templates/Items/weapons.yaml` (this is the
`TwoHanded` weapon the `warrior_guard_male_npc` [mobile template](mobile-templates.md)
equips):

```yaml
-   Id: halberd                 # unique key; referenced by mobile Equipment[].Item
    Name: Halberd
    Category: Weapons           # matched by item.create_by_category("Weapons")
    Description: ''
    ItemId: 5182                # base UO art id
    FlippableItemIds:           # item.flip cycles ItemId through this list
        - 5182
        - 5183
    Hue: 0
    GoldValue: 0                # declared, not consumed
    Weight: 16.0
    ScriptId: none              # copied onto the item; nothing dispatches on it yet
    IsMovable: true              # declared, not consumed
    Rarity: Common               # must be a defined ItemRarityType member
    Tags:
        - modernuo
        - weapons
        - flippable              # arbitrary; drives item.create_by_tag("flippable")
    Equip:
        Layer: TwoHanded          # paperdoll layer (layer_type.TwoHanded)
        HitPoints: 80
        StrengthReq: 95
        DexterityReq: 0
        IntelligenceReq: 0
    Weapon:
        LowDamage: 18
        HighDamage: 19
        Speed: 25
        BaseRange: 1
        MaxRange: 1
        HitSound: 567
        MissSound: 568
        WeaponSkill: Swords       # free-form skill name, not enum-validated
```

This entry has no `Container`, `Book`, `Params`, `LootTables`, `Stackable`,
`Dyeable`, `Visibility` or `LootType` keys — all optional and omitted here.
For real examples of those, see:

- `Container` — `armoire` and `backpack` in
  `src/Moongate.Server/Assets/Templates/Items/containers.yaml`.
- `Book` — `welcome_book` (via `BookId`) and `writable_book` (via `Params`)
  in `src/Moongate.Server/Assets/Templates/Items/base/books.yaml`.
- `LootTables` + `Params` together — `loot_test_chest` in
  `src/Moongate.Server/Assets/Templates/Items/base/test_containers.yaml`.
- `Stackable` — `agility_potion` in
  `src/Moongate.Server/Assets/Templates/Items/skill_items.yaml`.
- `Visibility` / `LootType` — `gm/gm_body.yaml` and `mounts.yaml`
  respectively.
