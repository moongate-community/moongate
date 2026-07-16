# Loot tables

Loot tables are the YAML source of truth for [`loot.roll`](../reference/loot.md#lootroll)
and for the `LootTableId` a mobile template (or one of its
[variants](mobile-templates.md#variants)) carries. They ship under
`src/Moongate.Server/Assets/Templates/Loot/` (recursively, including the
`creatures/` subfolder).

| Concern | Type |
|---|---|
| DTO | `LootTemplate` (`Moongate.UO.Data.Loot`) |
| Entry DTO | `LootTemplateEntry` (same namespace) |
| Mode enum | `LootTemplateModeType` (`Moongate.UO.Data.Types`): `Weighted`, `Additive` |
| Loader | `Moongate.Server.Loaders.LootTemplatesLoader` |
| Deserializer | `Moongate.Server.Services.Items.LootTemplateYamlDeserializer` |
| Validator | `Moongate.Server.Services.Items.LootTemplateValidator` |
| Roller | `Moongate.Server.Services.Items.LootService` |

This page documents every key the DTO binds and the exact rolling algorithm.
For a narrative walkthrough see the [Loot guide](../guides/loot.md) and the
[`loot` module reference](../reference/loot.md).

## File shape and identity

Each YAML file is a top-level list of loot table objects. The loader
concatenates every `*.yaml` file under the loot directory (case-insensitive
path order), validates the whole set together, then registers each table.

- `Id` must be present and **unique** (case-insensitive) across all files —
  a duplicate is a hard load error.
- `Name` and `Category` must be non-blank.
- `Entries` must contain **at least one** entry.
- `Rolls` must be `> 0`; `NoDropWeight` must be `>= 0`.
- `Mode` must be a defined `LootTemplateModeType` member (`Weighted` or
  `Additive`) — an explicit YAML null on `Mode`, `Rolls` or `NoDropWeight` is
  a shape error even though they have DTO defaults.
- Every entry's `ItemTemplateId` is checked against every loaded
  `ItemTemplate.Id`, and every entry's `ItemTag` is checked against the
  union of every loaded `ItemTemplate.Tags` — both case-insensitively. An
  unresolvable reference is a load error (loot tables are validated
  **after** item templates, using the full item set).

## Top-level keys

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Id` | `string` | required, unique | Table identifier — what `loot.roll`, item templates' `LootTables[]` and mobile templates' `LootTableId` reference. |
| `Name` | `string` | required | Display name (not surfaced to players; used for authoring clarity). |
| `Category` | `string` | required | Free-form grouping; by convention `loot` in shipped data, not enforced. |
| `Description` | `string` | optional, default `""` | Authoring note. |
| `Mode` | `LootTemplateModeType` enum | optional, default `Weighted` | `Weighted` or `Additive` — decides the whole rolling algorithm; see [Weighted vs. additive](#weighted-vs-additive). |
| `Rolls` | `int` | optional, default `1` | Number of times the table is rolled. Validator requires `> 0` (the roller's own `Math.Max(1, Rolls)` floor is defensive and unreachable via valid YAML). |
| `NoDropWeight` | `int` | optional, default `0` | Only meaningful in `Weighted` mode — an extra "nothing dropped" slice added to the weighted wheel. Validator requires `>= 0`. |
| `Entries` | `List<LootTemplateEntry>` | required, `>= 1` element | The candidate drops. See [LootTemplateEntry](#loottemplateentry). |

## LootTemplateEntry

Nested list items under `Entries:`. Every field is nullable on the DTO; which
ones are actually required depends on the table's `Mode`.

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Weight` | `int?` | **required when `Mode: Weighted`** (must be `> 0`); **forbidden when `Mode: Additive`** | This entry's share of the weighted wheel. |
| `Chance` | `double?` | **required when `Mode: Additive`** (must be finite, `0.0`–`1.0` inclusive); **forbidden when `Mode: Weighted`** | Independent probability this entry fires on a given roll. The roller falls back to `1.0` when absent, but the validator always requires it in `Additive` mode, so it is never actually absent in loaded data. |
| `ItemTemplateId` | `string?` | exactly one of `ItemTemplateId` / `ItemTag` required | A specific item template id (must exist). |
| `ItemTag` | `string?` | exactly one of `ItemTemplateId` / `ItemTag` required | A tag (must match at least one item template's `Tags`); the roller then creates a random item carrying that tag. |
| `Amount` | `int?` | a fixed amount **or** an `AmountMin`/`AmountMax` pair is required, never both; `Amount` alone must be `> 0` | Fixed quantity created. |
| `AmountMin` | `int?` | must be paired with `AmountMax`; both `> 0`; `AmountMin <= AmountMax` | Lower bound of a random quantity, rolled **inclusively**. |
| `AmountMax` | `int?` | must be paired with `AmountMin` | Upper bound of a random quantity, rolled **inclusively**. |

Setting both `ItemTemplateId` and `ItemTag` (or neither) is a load error;
same for setting both `Amount` and an `AmountMin`/`AmountMax` pair (or
neither).

## Weighted vs. additive

`Mode` decides how each of the `Rolls` passes reads `Entries`:

**`Weighted`** — each roll picks **at most one** entry from a wheel built
from `NoDropWeight` plus every entry's `Weight` (each entry's contribution is
floored at `1` defensively via `Math.Max(1, Weight)`, though the validator
already requires `Weight > 0`). A roll that lands in the `NoDropWeight`
slice produces nothing. `Rolls: N` repeats this pick `N` times
independently.

**`Additive`** — each roll walks **every** entry and independently includes
it when a fresh random draw is less than its `Chance`. `NoDropWeight` is
ignored in this mode (it is still validated as `>= 0`, but the roller never
reads it for `Additive` tables). `Rolls: N` repeats the whole walk `N`
times, so an entry with `Chance: 1.0` fires on every one of the `N` passes.

In both modes, once an entry is selected its `Amount` (fixed or
`AmountMin`..`AmountMax` inclusive range) decides how many are created via
the item factory, exactly the same way regardless of `Mode`.

## Full annotated example

`guard.archer`, from `src/Moongate.Server/Assets/Templates/Loot/guards.yaml`
— the table the `archer_guard_male_npc` / `archer_guard_female_npc`
[mobile templates](mobile-templates.md#full-annotated-example) carry as
`LootTableId`:

```yaml
-   Id: guard.archer                # referenced by LootTableId on the archer guard templates
    Name: GuardArcherLoot
    Category: loot
    Description: ''
    Mode: Additive                  # every entry below is evaluated independently
    Rolls: 1                        # the whole entry list is walked once
    NoDropWeight: 0                  # unused in Additive mode
    Entries:
        -   ItemTemplateId: gold     # must exist as an item template Id
            Chance: 1.0              # always included on the single roll
            AmountMin: 10            # random amount, inclusive
            AmountMax: 25
        -   ItemTemplateId: arrow
            Amount: 250              # fixed amount (mutually exclusive with AmountMin/Max)
            Chance: 1.0
```

Every roll of `guard.archer` therefore always drops both 10–25 gold and 250
arrows — there is no randomness in *whether* each entry fires (both have
`Chance: 1.0`), only in the gold amount. For a `Weighted` example with a
`NoDropWeight` "nothing" slice, see `undead.low` in the
[Loot guide](../guides/loot.md#anatomy-of-a-loot-table).
