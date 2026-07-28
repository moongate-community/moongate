# Starting items

`starting_items.yaml` is the YAML source of truth for what a brand-new
character is wearing and carrying the moment it enters the world. Unlike the
other data files it is a **single file**, not a folder:
`src/Moongate.Server/Assets/starting_items.yaml`.

| Concern | Type |
|---|---|
| DTO | `StartingItemsData` (`Moongate.UO.Data.StartingItems`) |
| Nested DTOs | `StartingItemKit`, `StartingItemEntry` (same namespace) |
| Loader | `Moongate.Server.Loaders.StartingItemsLoader` |
| Service | `Moongate.Server.Services.World.StartingItemsService` (`IStartingItemsService`) |
| Consumer | `Moongate.Server.Services.Accounts.CharacterService` |

There is no `startingitems.*` scripting module: this table is read exactly
once per character, during creation, and never again.

> [!IMPORTANT]
> `StartingItemsLoader` seeds `<root>/data/starting_items.yaml` **only when
> that file is missing**, and then parses the file on disk — never the
> embedded resource. Editing the tracked asset therefore changes nothing on a
> shard that already has a copy. Delete `<root>/data/starting_items.yaml` (or
> apply the same edit to it) to pick the new table up. Characters already
> created keep whatever they were given.

## File shape

One top-level mapping with three keys, all optional:

```yaml
All:                  # given to every character
    Equip: []
    Pack: []
ByBody:               # keyed "<Race>/<Gender>"
    Human/Male:
        Equip: []
        Pack: []
BySkill:              # keyed by skill name
    Blacksmithy:
        Equip: []
        Pack: []
```

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `All` | `StartingItemKit` | optional, default empty | Granted to every new character, whatever the race, gender or skills. |
| `ByBody` | `Dictionary<string, StartingItemKit>` | optional, default empty | Keyed `"<Race>/<Gender>"`. At most one entry ever matches. |
| `BySkill` | `Dictionary<string, StartingItemKit>` | optional, default empty | Keyed by skill name. Up to three entries match. |

Both dictionaries are `OrdinalIgnoreCase`, so `human/male` and `Human/Male`
are the same key. A key that matches nothing simply contributes nothing —
there is no fallback kit and no load error.

### ByBody keys

The key is built as `$"{race}/{gender}"` from the enum member names, giving
exactly six valid combinations:

`Human/Male`, `Human/Female`, `Elf/Male`, `Elf/Female`, `Gargoyle/Male`,
`Gargoyle/Female`.

(`RaceType` and `GenderType` live in `Moongate.UO.Data.Types`; an
unrecognised race/gender byte on the wire already decoded to `Human/Male`
before this table is consulted.)

### BySkill keys

The key is a **skill name** as spelled in `Assets/skills.yaml` —
`Blacksmithy`, `Animal Lore`, `Bowcraft/Fletching`, and so on. It is *not* a
skill id.

Which three keys are tried is decided by the creation packet, in
`CharacterService`:

1. take the four skills the client sent,
2. drop those with a value of `0`,
3. order by value descending,
4. take the top three,
5. map each id back to its name through `ISkillService`.

A skill whose id resolves to no skill is dropped. Ties are resolved by the
order the client sent them, which is the order they appear on the creation
screen.

## StartingItemKit

Both `All` and every dictionary value are a kit — two independent lists,
both optional and both defaulting to empty:

| Key | Type | Meaning |
|---|---|---|
| `Equip` | `List<StartingItemEntry>` | Worn on the paperdoll. The item template must declare an [`Equip.Layer`](item-templates.md#equipspec) or the entry is skipped. |
| `Pack` | `List<StartingItemEntry>` | Dropped into the backpack at a random free-looking position (x 44–139, y 65–139). |

Matching kits are **concatenated, not merged**: `All`, then the `ByBody` kit,
then one `BySkill` kit per top skill, in skill order. Nothing is deduplicated
and nothing overrides anything.

> [!WARNING]
> Two entries that equip the same layer are both created and both persisted,
> but only the last one stays on the mobile — `EquippedItemIds[layer]` is
> overwritten, and the displaced item is no longer reachable from the
> character. Watch for this when a `BySkill` kit equips a garment the
> `ByBody` kit already covers (several skill kits hand out a `robe`).

The backpack and the bank box are **not** in this table: `CharacterService`
equips them from the `backpack` and `bank_box` templates before the kit is
resolved, and if the backpack is missing the `Pack` lists are skipped
entirely.

## StartingItemEntry

| Key | Type | Required / default | Meaning |
|---|---|---|---|
| `Item` | `string` | required | An [item template](item-templates.md) `Id`. A template that does not exist logs a warning and the entry is skipped. |
| `Amount` | `int` | optional, default `1` | Stack size, floored at `1`. Passed for `Equip` entries too. |
| `Hue` | `string?` | optional, default `null` | Hue token — see [Hue tokens](#hue-tokens). Absent or empty means the item keeps the hue from its own template. |
| `Newbie` | `bool` | optional, default `false` | Bound by the DTO but **not read by anything yet** — there is no blessed/newbie item mechanic in this codebase. |

Unlike [loot tables](loot-tables.md) and [item templates](item-templates.md),
this file has **no validator**: a bad `Item` is a runtime warning at
character creation, not a load error. A data test does check that every
referenced template resolves, so a typo fails CI rather than production.

### Hue tokens

`Hue` is a string, and it is matched in this order (case-insensitive):

| Value | Result |
|---|---|
| absent, or `""` | No override — the item keeps its template's own `Hue`. |
| `shirt` | The shirt colour picked on the character-creation screen. |
| `pants` | The pants colour picked on the character-creation screen. |
| anything else | Parsed as a **hexadecimal** number and used as a literal hue. Unparseable values fall back to the template's hue. |

> [!CAUTION]
> The literal branch always parses as hex, whether or not you write the `0x`
> prefix. `Hue: "1000"` is hue `0x1000` (4096), not 1000. Write literals as
> quoted `"0x..."` — that is what every shipped entry does — so the intent is
> unambiguous.

`shirt` and `pants` are the only two tokens that read the player's choices;
they come straight off the character-creation packet. A garment that omits
them is created colourless, which is why the shipped body kits declare them
on every shirt, pair of pants and skirt. A gargoyle's robe stands in for both
garments, so it takes the shirt hue.

## Full annotated example

```yaml
# Everyone gets these, regardless of who they are.
All:
    Pack:
        -   Item: dagger
        -   Item: my_story

# At most one of these matches, on "<Race>/<Gender>".
ByBody:
    Human/Male:
        Equip:
            -   Item: long_pants
                Hue: pants        # the colour picked on the creation screen
            -   Item: shirt
                Hue: shirt
            -   Item: shoes       # no Hue: keeps the template's own hue

# Up to three of these match, on the character's three highest skills.
BySkill:
    Blacksmithy:
        Equip:
            -   Item: half_apron
                Hue: "0x6be"      # literal hue, always parsed as hex
            -   Item: leather_gloves
            -   Item: runic_hammer
        Pack:
            -   Item: iron_ingot
                Amount: 50
            -   Item: shovel
```

A human male smith created with this table enters the world wearing his
chosen pants and shirt plus shoes, an apron, gloves and a hammer, carrying a
dagger, a book, fifty ingots and a shovel.
