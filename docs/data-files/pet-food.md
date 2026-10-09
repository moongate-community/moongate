# Pet food

`pet_food.toml` lists the items a pet eats, by kind of food. A player feeds a pet by dragging one of them on it: the pet
eats the whole stack when its creature eats that kind (the `food` of [`taming.toml`](taming.md)) and its loyalty rises:
see [animal taming](../animal-taming.md#loyalty-food-and-obedience). Without the file no pet eats.

```toml
[[food]]
kind = "fruit"
items = ["0x09d0_apple", "0x0c77_carrot"]
```

| Field | Meaning |
| --- | --- |
| `[[food]]` | One per kind of food. |
| `kind` | `meat`, `fruit` (fruit and vegetables), `grain`, `fish` or `eggs`. A kind is there once. |
| `items` | The ids of the item templates that are food of this kind. Each must exist. |

A bad value stops the server at startup, naming it. The shipped file has the cooked meats, the fruits and vegetables and the
bowls of them, breads, fish and fried eggs.
