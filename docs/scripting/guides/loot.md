# Loot

This guide reads a loot table from YAML, explains how the roller turns it into
items, and ends with a script that rolls a table and drops the result into a
corpse's pack. It uses the [`loot`](../reference/loot.md) module and a real loot
table from `src/Moongate.Server/Assets/Templates/Loot/`.

## Anatomy of a loot table

A loot table is a YAML entry with an `Id`, a `Mode`, a number of `Rolls`, and a
list of `Entries`. Here is the real `undead.low` table:

```yaml
-   Id: undead.low
    Mode: Weighted
    Rolls: 1
    NoDropWeight: 3
    Entries:
        -   Weight: 5
            ItemTemplateId: gold
            Amount: 45
        -   Weight: 2
            ItemTemplateId: bandage
            Amount: 4
```

Each entry names exactly one of:

- **`ItemTemplateId`** — a specific item template (`gold`, `bandage`, …), or
- **`ItemTag`** — a tag, from which the roller picks a random tagged item.

An entry setting both, or neither, is skipped with a warning. Amount is either a
fixed `Amount`, or a range with `AmountMin` / `AmountMax` (rolled inclusively).

## Weighted vs additive

The `Mode` decides how each roll reads the entries.

**Weighted** (the mode `undead.low` uses) picks **one** entry per roll, by
weight. `NoDropWeight` adds a "nothing dropped" slice to the wheel. For
`undead.low` the weights are `NoDropWeight 3 + gold 5 + bandage 2 = 10`, so a
single roll is:

| Outcome | Chance |
|---|---|
| nothing | 3/10 |
| 45 gold | 5/10 |
| 4 bandages | 2/10 |

**Additive** instead walks **every** entry and includes each independently with
its own `Chance` (a probability from `0.0` to `1.0`, defaulting to `1.0`). The
creature tables under `Loot/creatures/` use this mode — each line is an
independent drop:

```yaml
    Mode: Additive
    Entries:
        -   ItemTemplateId: gold
            AmountMin: 50
            AmountMax: 150
            Chance: 1.0
        -   ItemTag: gems
            Amount: 1
            Chance: 0.25
```

Here every roll always drops 50–150 gold and drops a random gem a quarter of the
time.

**`Rolls`** repeats the whole process: a weighted table with `Rolls: 2` picks
twice; an additive table with `Rolls: 2` walks its entries twice.

## Rolling from Lua

[`loot.roll`](../reference/loot.md#lootroll) takes a table id and returns an
array-table of the serials it created. An unknown id is not an error — it logs a
warning and returns an **empty** table, so a loop over the result is always
safe.

> [!IMPORTANT]
> Rolled items are created **floating** (unplaced). You place them yourself, for
> example into a container with
> [`item.add_to_container`](../reference/item.md#itemadd_to_container).

## The complete script

This spawns a skeletal knight, then rolls the `undead.low` table into a backpack
standing in for its corpse pack.

```lua
-- scripts/main.lua

events.on("world_ready", function()
  local knight = mobile.create_from_template("skeletal_knight_npc", 1, 1425, 1695, 0)
  if knight then
    log.info("skeletal knight {0} spawned", knight)
  end

  -- A backpack to act as the corpse's loot container.
  local corpse = item.create("backpack", 1, 0)

  -- Roll a loot table and drop every result into the pack.
  local drops = loot.roll("undead.low")
  for _, serial in ipairs(drops) do
    local loot_item = item.get(serial)
    item.add_to_container(corpse, serial, 0, 0)
    log.info("dropped {0} x{1}", loot_item.name, loot_item.amount)
  end

  if #drops == 0 then
    log.debug("undead.low rolled nothing this time")
  end
end)
```

Because `undead.low` has a `NoDropWeight`, an empty roll is a normal outcome —
the script handles it rather than treating it as an error.
