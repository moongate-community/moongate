# Enums

Moongate exposes several C# enums to Lua as **read-only, case-insensitive**
global tables. Each table maps a member name to its numeric value (for example
`skill_name.Swordsmanship == 40`). The global name is the snake_case form of the
C# enum type name.

| Global | C# enum | Used by |
|---|---|---|
| `skill_name` | `SkillName` | [`mobile.get_skill`](mobile.md#mobileget_skill) / [`set_skill`](mobile.md#mobileset_skill) / [`skills`](mobile.md#mobileskills) |
| `gender_type` | `GenderType` | [`mobile.set`](mobile.md#mobileset) (`gender`) |
| `race_type` | `RaceType` | [`mobile.set`](mobile.md#mobileset) (`race`) |
| `layer_type` | `LayerType` | [`item.equip`](item.md#itemequip) / [`unequip`](item.md#itemunequip) |
| `account_level_type` | `AccountLevelType` | [`account.create`](account.md#accountcreate) / [`set_level`](account.md#accountset_level) |

Wherever a function takes one of these, it also accepts the equivalent name
string, so `item.equip(m, s, layer_type.OneHanded)` and
`item.equip(m, s, "OneHanded")` are equivalent. The tables cannot be extended,
and reading an undefined member logs a warning.

## skill_name

The canonical Ultima Online skills. Reference them by constant for typo-safe,
readable code.

| Constant | Value | Constant | Value |
|---|---|---|---|
| `Alchemy` | 0 | `Poisoning` | 30 |
| `Anatomy` | 1 | `Archery` | 31 |
| `AnimalLore` | 2 | `SpiritSpeak` | 32 |
| `ItemIdentification` | 3 | `Stealing` | 33 |
| `ArmsLore` | 4 | `Tailoring` | 34 |
| `Parrying` | 5 | `AnimalTaming` | 35 |
| `Begging` | 6 | `TasteIdentification` | 36 |
| `Blacksmithy` | 7 | `Tinkering` | 37 |
| `BowcraftFletching` | 8 | `Tracking` | 38 |
| `Peacemaking` | 9 | `Veterinary` | 39 |
| `Camping` | 10 | `Swordsmanship` | 40 |
| `Carpentry` | 11 | `MaceFighting` | 41 |
| `Cartography` | 12 | `Fencing` | 42 |
| `Cooking` | 13 | `Wrestling` | 43 |
| `DetectingHidden` | 14 | `Lumberjacking` | 44 |
| `Discordance` | 15 | `Mining` | 45 |
| `EvaluatingIntelligence` | 16 | `Meditation` | 46 |
| `Healing` | 17 | `Stealth` | 47 |
| `Fishing` | 18 | `RemoveTrap` | 48 |
| `ForensicEvaluation` | 19 | `Necromancy` | 49 |
| `Herding` | 20 | `Focus` | 50 |
| `Hiding` | 21 | `Chivalry` | 51 |
| `Provocation` | 22 | `Bushido` | 52 |
| `Inscription` | 23 | `Ninjitsu` | 53 |
| `Lockpicking` | 24 | `Spellweaving` | 54 |
| `Magery` | 25 | `Mysticism` | 55 |
| `ResistingSpells` | 26 | `Imbuing` | 56 |
| `Tactics` | 27 | `Throwing` | 57 |
| `Snooping` | 28 | | |
| `Musicianship` | 29 | | |

**Example**

```lua
mobile.set_skill(guard, skill_name.Swordsmanship, 900)
```

## gender_type

| Constant | Value |
|---|---|
| `Male` | 0 |
| `Female` | 1 |

**Example**

```lua
mobile.set(guard, { gender = gender_type.Male })
```

## race_type

| Constant | Value |
|---|---|
| `Human` | 0 |
| `Elf` | 1 |
| `Gargoyle` | 2 |

**Example**

```lua
mobile.set(guard, { race = race_type.Human })
```

## layer_type

The UO equipment layers. `None` (0) means "not a wearable layer".

| Constant | Value | Constant | Value |
|---|---|---|---|
| `None` | 0 | `InnerTorso` | 13 |
| `OneHanded` | 1 | `Bracelet` | 14 |
| `TwoHanded` | 2 | `Face` | 15 |
| `Shoes` | 3 | `FacialHair` | 16 |
| `Pants` | 4 | `MiddleTorso` | 17 |
| `Shirt` | 5 | `Earrings` | 18 |
| `Helm` | 6 | `Arms` | 19 |
| `Gloves` | 7 | `Cloak` | 20 |
| `Ring` | 8 | `Backpack` | 21 |
| `Talisman` | 9 | `OuterTorso` | 22 |
| `Neck` | 10 | `OuterLegs` | 23 |
| `Hair` | 11 | `InnerLegs` | 24 |
| `Waist` | 12 | `Mount` | 25 |

Layers 26–29 (`ShopBuy`, `ShopResale`, `ShopSell`, `Bank`) exist for vendor and
bank storage and are not normal equip targets.

**Example**

```lua
item.equip(guard, blade, layer_type.OneHanded)
```

## account_level_type

An account's privilege level.

| Constant | Value |
|---|---|
| `Player` | 0 |
| `GrandMaster` | 1 |
| `Administrator` | 2 |

**Example**

```lua
account.set_level("tom", account_level_type.GrandMaster)
```
