# Mobile templates

Mobile templates are the YAML source of truth for every id
`mobile.create_from_template` can spawn. They ship under
`src/Moongate.Server/Assets/Templates/Mobiles/` (recursively, including the
`base/` subfolder), and unlike item templates they support a real
inheritance step (`BaseMobile`) resolved at load time.

| Concern | Type |
|---|---|
| DTO | `MobileTemplate` (`Moongate.UO.Data.Mobiles.Templates`) |
| Nested DTOs | `MobileAppearance`, `MobileEquipmentEntry`, `MobileVariant` (same namespace) |
| Loader | `Moongate.Server.Loaders.MobileTemplatesLoader` |
| Deserializer | `Moongate.Server.Services.Mobiles.MobileTemplateYamlDeserializer` |
| Base-merge resolver | `Moongate.Server.Services.Mobiles.MobileTemplateBaseResolver` |
| Spawn-time factory | `Moongate.Server.Services.Mobiles.MobileFactoryService` |

This page documents every key the DTO binds. For a walkthrough of spawning
and reading mobiles see the [Mobiles guide](../guides/mobiles.md) and the
[`mobile` module reference](../reference/mobile.md).

> [!NOTE]
> There is no `MobileTemplateValidator`. Beyond the deserializer's own
> null/duplicate-key checks, the loader does not reject a duplicate `Id` —
> the last template registered for a given `Id` (case-insensitive) silently
> wins, both while resolving `BaseMobile` and in the final template
> dictionary. Unknown skill names and unknown equipment layers are also not
> load errors: they are resolved lazily at spawn time and simply skipped
> with a warning if they don't match (see [Skills](#skills) and
> [Equipment](#equipment) below).

## File shape and `BaseMobile` resolution

Each YAML file is a top-level list of mobile template objects. The loader
concatenates every `*.yaml` file under the mobiles directory (case-insensitive
path order) into one flat list, then `MobileTemplateBaseResolver` resolves
`BaseMobile` references before anything is registered.

- `BaseMobile: <id>` merges the named template's fields onto the current
  one — the current (derived) template wins per the per-field rules in the
  tables below. Bases can themselves have a `BaseMobile` (multi-level
  inheritance is resolved recursively).
- An unknown `BaseMobile` id, or an inheritance cycle, is a load error
  (`InvalidDataException`).
- The **resolved** template's `BaseMobile` is always cleared to `null` —
  once merged, a template no longer carries a base reference.

## Top-level keys

| Key | Type | Required / default | Merge rule when `BaseMobile` is set |
|---|---|---|---|
| `Id` | `string` | required (no format check) | Not merged — the derived `Id` is kept. |
| `Name` | `string` | optional, default `""` | Derived value wins if non-empty, else the base's. |
| `Gender` | `MobileTemplateGenderType` enum | optional, default `Male` | Derived value wins **only if it is not `Male`**; see [Gender](#gender) for the caveat this creates. |
| `Title` | `string` | optional, default `""` | Derived wins if non-empty, else base's. Displayed under the mobile's name (e.g. "the guard"). |
| `Category` | `string` | optional, default `""` | Derived wins if non-empty, else base's. Matched by `GetByCategory`. |
| `Description` | `string` | optional, default `""` | Derived wins if non-empty, else base's. |
| `Tags` | `List<string>` | optional, default `[]` | **Concatenated**: base tags then derived tags, de-duplicated case-insensitively — not a wholesale replace. |
| `BaseMobile` | `string?` | optional, default `null` | The id to merge onto. Always `null` on the resolved output. |
| `Strength` | `int` | optional, default `50` | Derived wins **only if it differs from `50`** — see the same caveat as `Gender`. |
| `Dexterity` | `int` | optional, default `50` | Same rule as `Strength`. |
| `Intelligence` | `int` | optional, default `50` | Same rule as `Strength`. |
| `Skills` | `Dictionary<string, int>` (case-insensitive keys) | optional, default `{}` | Real union: base entries are copied in first, then derived entries are applied on top — a derived template can add or override individual skills without repeating the whole base set. See [Skills](#skills). |
| `Appearance` | `MobileAppearance` | optional, default `new()` | Merged per-field — see [MobileAppearance](#mobileappearance). |
| `Equipment` | `List<MobileEquipmentEntry>` | optional, default `[]` | **Wholesale replace**: if the derived template declares any equipment at all, the entire base list is discarded (not merged item-by-item). |
| `Variants` | `List<MobileVariant>` | optional, default `[]` | **Wholesale replace**, same rule as `Equipment`. See [Variants](#variants). |
| `LootTableId` | `string?` | optional, default `null` | Derived value wins if set, else the base's (`derived ?? base`). |
| `BrainScript` | `string?` | optional, default `null` | Derived value wins if set, else the base's (`derived ?? base`). **Reserved** — copied onto the spawned mobile's `BrainScriptId` but no AI dispatcher in this codebase reads it yet. |

### Gender

`MobileTemplateGenderType` has three members: `Male` (0, the default),
`Female` (1), `Random` (2). At spawn time `MobileFactoryService` resolves it
to an actual gender:

| Template `Gender` | Spawned gender |
|---|---|
| `Male` | always male |
| `Female` | always female |
| `Random` | coin-flip (50/50) per spawn |

No shipped template currently sets `Gender: Random`; `Female` is used (e.g.
`archer_guard_female_npc`, `warrior_guard_female_npc` in
`Mobiles/guards.yaml`). `Male` doubles as both "explicitly male" and "not
set", which has one consequence worth calling out: **because the merge rule
only takes the derived value when it is not `Male`, a derived template
cannot force a base's `Female`/`Random` gender back to `Male` — omitting
`Gender` and writing `Gender: Male` are indistinguishable, and both mean
"inherit the base's gender" whenever `BaseMobile` is set.**

The same caveat applies to `Strength`/`Dexterity`/`Intelligence`: because
`50` is both the DTO default and a legitimate explicit value, a derived
template cannot use `50` to override a non-`50` base stat — it will always
inherit the base's stat in that case.

### Skills

`Skills` values are skill points **in tenths** (`Swordsmanship: 1200` means
a starting skill of `120.0`). Keys are matched against the `SkillName` enum
(see [`skill_name`](../reference/enums.md#skill_name)) at **spawn** time, not
load time: `MobileFactoryService` strips everything but letters from the key
and parses the remainder case-insensitively, so `"Detecting Hidden"` and
`DetectingHidden` both resolve. An unresolved key is not a load or spawn
error — it is logged as a warning and that skill is skipped.

### MobileAppearance

Nested under `Appearance:`.

| Key | Type | Required / default | Merge rule |
|---|---|---|---|
| `Body` | `int` | optional, default `0` | Derived wins only if non-zero, else base's. UO body/graphic id. |
| `SkinHue` | `string?` | optional, default `null` | `derived ?? base`. A [hue spec](#hue-specs). |
| `HairStyle` | `int` | optional, default `0` | Derived wins only if non-zero, else base's. `0` = no hair. |
| `HairHue` | `string?` | optional, default `null` | `derived ?? base`. A [hue spec](#hue-specs). |
| `FacialHairStyle` | `int` | optional, default `0` | Derived wins only if non-zero, else base's. `0` = no facial hair. |
| `FacialHairHue` | `string?` | optional, default `null` | `derived ?? base`. A [hue spec](#hue-specs). |

The same variant-level override applies again at spawn time: a
[`Variants`](#variants) entry's `Appearance` fields override the template's
per-field, using the identical zero/null-means-fallback rule.

#### Hue specs

`SkinHue`, `HairHue`, `FacialHairHue` (and `MobileEquipmentEntry.Hue`) all
share one small grammar, resolved by `HueSpec.Resolve` at spawn time:

| Written as | Resolves to |
|---|---|
| omitted, blank, or unparsable | `0` |
| a plain integer, e.g. `'1153'` | that hue, verbatim |
| `'hue(a:b)'`, e.g. `'hue(1002:1058)'` | a random integer in `[a, b]` inclusive, re-rolled on every spawn |

### Equipment

`Equipment` (top-level) and `MobileVariant.Equipment` are both lists of
`MobileEquipmentEntry`:

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Item` | `string` | optional, default `""` | An item template `Id`. Created via the item factory when the mobile is spawned (see [item templates](item-templates.md)). |
| `Layer` | `string` | optional, default `""` | Parsed case-insensitively into a [`LayerType`](../reference/enums.md#layer_type) at spawn time. An unresolved layer name is not a load error — it is logged as a warning and that entry is skipped. |
| `Hue` | `string?` | optional, default `null` | A [hue spec](#hue-specs); resolved independently per equipment entry. |

### Variants

A template may declare weighted **variants** — alternates picked once per
spawn — under `Variants:`:

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Name` | `string` | optional, default `""` | Descriptive label only; not referenced elsewhere. |
| `Weight` | `int` | optional, default `1` | Share of the weighted pick among all variants (floored at `1` even if written lower). |
| `Gender` | `MobileTemplateGenderType?` | optional, default `null` | Overrides the template's `Gender` for this variant when set. |
| `LootTableId` | `string?` | optional, default `null` | Overrides the template's `LootTableId` for this variant when set. |
| `Appearance` | `MobileAppearance` | optional, default `new()` | Per-field override of the template's `Appearance` (same zero/null-means-fallback rule as the base merge). |
| `Equipment` | `List<MobileEquipmentEntry>` | optional, default `[]` | Wholesale replace of the template's `Equipment` when non-empty. |

At spawn time `MobileFactoryService` picks one variant by weight (each
variant's share is `Math.Max(1, Weight)` out of the sum of all shares), then
overlays its `Gender`/`LootTableId`/`Appearance`/`Equipment` onto the
template's own. **No shipped template currently declares `Variants`** — the
mechanism is fully wired but unused in the current data set.

## Full annotated example

`base_human_npc` (the base) and `archer_guard_female_npc` (a derived
template that uses it), both from
`src/Moongate.Server/Assets/Templates/Mobiles/`:

```yaml
# src/Moongate.Server/Assets/Templates/Mobiles/base/human.yaml
-   Id: base_human_npc
    Title: a human
    Category: npc
    Description: Base human NPC template for derived vendor/citizen profiles.
    Tags:
        - npc
        - human
        - base
    Strength: 50            # the DTO default; the derived template below overrides all three
    Dexterity: 50           # since its own Strength/Dexterity/Intelligence differ from 50
    Intelligence: 50
    Appearance:
        Body: 400
        SkinHue: 'hue(1002:1058)'
        HairStyle: 8251
        HairHue: 'hue(1102:1149)'
    Equipment:
      - Item: shirt
        Layer: Shirt
      - Item: long_pants
        Layer: Pants
      - Item: shoes
        Layer: Shoes
```

```yaml
# src/Moongate.Server/Assets/Templates/Mobiles/guards.yaml
-   Id: archer_guard_female_npc
    Title: the guard
    Category: npc
    Gender: Female                          # base has no Gender -> Female wins
    Description: Town female archer guard NPC inspired by ModernUO ArcherGuard.
    Tags:
        - npc
        - guard
        - archer
        - town
        - female                            # concatenated with base's [npc, human, base]
    BaseMobile: base_human_npc
    Strength: 100                            # != 50 -> overrides the base's 50
    Dexterity: 125
    Intelligence: 25
    Skills:
        Anatomy: 1200                        # 120.0
        Tactics: 1200
        Archery: 1200
        ResistingSpells: 1200
        DetectingHidden: 1000
    Appearance:
        Body: 401                            # != 0 -> overrides the base's 400
        SkinHue: 'hue(1002:1058)'
        HairStyle: 8251
        HairHue: 'hue(1102:1149)'
    Equipment:                                # non-empty -> replaces the base's shirt/pants/shoes wholesale
      - Item: studded_chest
        Layer: InnerTorso
      - Item: studded_arms
        Layer: Arms
      - Item: studded_gloves
        Layer: Gloves
      - Item: studded_gorget
        Layer: Neck
      - Item: studded_legs
        Layer: Pants
      - Item: boots
        Layer: Shoes
      - Item: skull_cap
        Layer: Helm
      - Item: bow
        Layer: TwoHanded
    LootTableId: guard.archer                 # see the Loot tables page
    BrainScript: guard
```

After resolution, `archer_guard_female_npc` keeps `base_human_npc`'s
`Title`/`Category`/`Description` fallback shape but ends up with its own
`Strength`/`Dexterity`/`Intelligence`, `Body`, full `Tags` union, and a
completely replaced `Equipment` list — while `BaseMobile` itself is cleared
on the merged result. `guard.archer` (its `LootTableId`) is documented as
the [full example on the Loot tables page](loot-tables.md#full-annotated-example).
