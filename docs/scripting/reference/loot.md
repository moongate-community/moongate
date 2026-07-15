# loot

Rolls loot tables into items. Backed by `LootModule`.

Loot table ids come from the loot templates under
`src/Moongate.Server/Assets/Templates/Loot/`.

> [!IMPORTANT]
> Rolled items are created **floating** (unplaced). Place them yourself, for
> example with [`item.add_to_container`](item.md#itemadd_to_container).

## loot.roll

```lua
loot.roll(lootTableId) -> table of serials
```

Rolls the loot table with the given id, creates the resulting items and returns
their serials as an array-table. When the table id is unknown, the call logs a
warning and returns an **empty** table — it does not raise an error.

**Example**

```lua
local pack = item.create("backpack", 1, 0)
for _, serial in ipairs(loot.roll("undead.low")) do
  item.add_to_container(pack, serial, 0, 0)
end
```
