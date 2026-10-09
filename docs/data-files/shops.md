# Shops

A shop says what a kind of vendor sells. One file under `templates/shops/` holds one or more shops, and the server
loads them at startup after the item and mobile templates. The shipped shops are the ones of ModernUO, one file for
each kind of vendor: `baker.toml`, `blacksmith.toml`, `mage.toml` and so on.

```toml
[[shop]]
id = "baker"
vendors = ["baker", "m_baker", "f_baker"]

[[shop.buy]]
item = "0x103b_bread_loaf"
price = 6
amount = 20
hue = 0
name = ""
```

| Field | Meaning |
| --- | --- |
| `id` | The stable id of the shop. It is unique across the files. |
| `vendors` | The ids of the mobile templates whose vendors use the shop. A template is in one shop only. |
| `item` | The id of an item template. |
| `price` | Gold for one piece, at least 1. |
| `amount` | How many pieces the vendor starts with, at least 1. |
| `hue` | The hue of the goods. 0 keeps the one of the item template. |
| `name` | The name shown in the shop window. Empty: the client's name for the graphic. |

A shop may also have `[[shop.sell]]` lines for what a vendor buys from a player. They have an `item` and a `price`, the
gold paid for one piece; `amount`, `hue` and `name` are ignored. The same item may be both sold and bought, at
different prices.

The server refuses to start, naming the file and the shop, when a shop has no id, an id is used twice, a line names
an item template that does not exist, a price is under 1, an amount is outside 1 to 60000, a hue is outside 0 to
65535, a name is not ASCII or is over 253 characters, two buy lines have the same item, price and hue, a vendor is not a
mobile template, or a vendor is in two shops. A vendor template only needs the `shopkeeper` script, which `basevendor` already gives, to
open its window: see [Vendors](../vendors.md).

## Add or change a shop

1. Copy a shipped file to `<root>/templates/shops/`, or edit it in place.
2. Give the shop an id, list the mobile templates that use it and write its lines.
3. Restart the server. A mistake stops it with a message that names the line.

## Convert ModernUO's shops

```bash
cd tools/convert
uv run moongate-convert modernuo-vendors --source ~/projects/others/ModernUO/Projects/UOContent \
  --items ../../moongate_root/templates/items --mobiles ../../moongate_root/templates/mobiles \
  --destination ../../moongate_root/templates/shops
```

The converter reads the C# as syntax and runs nothing. It takes the lines of the `SBInfo` classes that each vendor
class adds, and writes one file for each vendor class, named after it. A line becomes the item template with the
graphic ModernUO gives it; when several templates share a graphic, the one named like the C# type wins, else the plain
piece of the first era the graphic has (`lbr`, then `aos`, `t2a`, `tol`), else the first one, and the report says so. A weapon or a tool has two graphics, one for each way it faces: when no template has the graphic ModernUO sells it under, the converter takes the templates of the other graphic of the pair (the `Flippable` of the item class, and a few pairs it knows), else the one template named like the type. A
graphic that has only material variants (agapite, bronze and so on) and no plain piece is sold as the plain piece of the other graphic of its pair (the metal helmets), and left out of the buy lines when that has none either. A
vendor buys a piece whatever it is made of, so the sell lines of an armor or weapon graphic list every era and material
template. The report counts what it left out: types with no item template, pets, lines
and `SBInfo` classes that depend on the era or the vendor, and vendor classes with no mobile template. A `switch`
that picks a random set of `SBInfo` for each vendor is read as the union of its sets. A sell price above the lowest price any vendor asks for the same item is lowered to
it, so that buying from one vendor and selling to another is never a profit.
