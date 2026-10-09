# Taming

`taming.toml` lists the creatures a player can tame with the [Animal Taming](../animal-taming.md) skill. A creature with no
entry cannot be tamed. Without the file nothing can.

```toml
[[creature]]
template = "horse"
min_skill = 29.1
slots = 1
food = ["fruit", "grain"]
```

| Field | Meaning |
| --- | --- |
| `[[creature]]` | One per creature that can be tamed. |
| `template` | The id of its mobile template. It must exist, and be there once. |
| `min_skill` | The Animal Taming it takes, in points, from -50 to 120. A try has a chance from 0.1 under it to 49.9 above. The small animals ask less than none. |
| `slots` | How many followers it counts for, 1 to 10; 1 when left out. |
| `food` | The kinds of food it eats once tamed, from `meat`, `fruit`, `grain`, `fish` and `eggs`; `["meat"]` when left out, `[]` for one that eats none of them. The items of each kind are in [`pet_food.toml`](pet-food.md). |

A bad value stops the server at startup, naming the creature.

The file is generated from the creature classes of another emulator (`Tamable`, `MinTameSkill`, `ControlSlots`, `FavoriteFood`):

```sh
cd tools/convert
uv run moongate-convert modernuo-taming --source <ModernUO>/Projects/UOContent \
  --templates ../../moongate_root/templates/mobiles --destination ../../moongate_root/data
```

A class with no template of this server is skipped and counted; the three coats of the horse (`brownhorse`, `grayhorse`,
`darkhorse`) take the skill of the horse.
