# Starting items

`starting_items.toml` holds the items a new character gets. The shipped file is written
by [`mg-uoxconv`](../uox3-migration.md#starting-items) from UOX3's `newbie.dfn`.
`IStartingItemsService.GiveAsync` applies it to a new character.

A character gets every `[[set]]` with `common = true`, plus every set whose filters it
matches.

```toml
[[set]]
skill = "alchemy"
[[set.items]]
items = ["0x0f7a_black_pearl"]
amount = 3
equip = false

[[set.items]]
items = ["0x1f03_robe"]
hue = 1226
equip = true
```

| Field | Meaning |
| --- | --- |
| `common` | `true` gives the set to every character, whatever the filters |
| `skill` | Given to characters starting with this skill among their best ones |
| `race` | `human`, `elf` or `gargoyle`; unset is every race |
| `gender` | `male` or `female`; unset is both |
| `items` | Item template ids; one is picked at random |
| `amount` | How many, as dice; unset is 1 |
| `hue` | The hue to give the item; unset keeps its own |
| `equip` | `true` puts the item on the character, `false` in the backpack |
| `newbie` | `false` makes the item drop on death; unset or `true` makes it `Newbied` (kept) |

`GiveAsync` works in one transaction on the world database; if anything fails, the
character gets nothing:

1. It takes the character's `ultima.starting_items.best_skills` highest skills (default 3; ties
   go to the lower skill id; skills at 0 do not count).
2. It applies, in order, the sets of those skills, the common sets, then the sets of
   the character's race and gender.
3. It creates the backpack (`ultima.items.backpack_template`) and puts it on the
   `Backpack` layer.
4. For each entry it picks one item. A stackable item gets the whole `amount`;
   any other item is created `amount` times.
5. `equip = true` wears the item on its layer: the template's `layer`, or else the
   client's tiledata layer when tiledata marks the graphic wearable. If there is no layer, or the layer is taken, the item goes
   in the backpack instead, so the first item on a layer wins.
6. Worn shirts and robes take the shirt hue picked at creation, pants and skirts the
   pants hue; a hue of 0 keeps the item's own.
7. Items in the backpack go to a random spot inside its `containers.toml` bounds.

The starting gold is an ordinary entry: the shipped file gives 1000 coins with an
entry of the common set. Change its `amount` to give more or less, or remove the
entry to give none:

```toml
[[set]]
common = true
[[set.items]]
items = ["0x0eed_gold_coin"]
amount = 1000
equip = false
```

## Validation at startup

The server stops when:

- `starting_items.toml` does not exist;
- a set has no items, or is not common and has no skill, race or gender;
- an entry has no items, names an item that is not an item template, or has an
  `amount` that can roll below 1;
- `ultima.items.backpack_template` or `ultima.items.gold_template` is not an item template;
- the gold template does not stack.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
