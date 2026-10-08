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

A shop may also have `[[shop.sell]]` lines, with the same fields, for what a vendor will buy from a player. Nothing
reads them yet.

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
mgctl convert modernuo-vendors --source ~/projects/others/ModernUO/Projects/UOContent \
  --items moongate_root/templates/items --mobiles moongate_root/templates/mobiles \
  --destination moongate_root/templates/shops
```

The converter reads the C# as syntax and runs nothing. It takes the lines of the `SBInfo` classes that each vendor
class adds, and writes one file for each vendor class, named after it. A line becomes the item template with the
graphic ModernUO gives it; when several templates share a graphic, the one named like the C# type wins, else the first
one is taken and the report says so. The report counts what it left out: types with no item template, pets, lines
and `SBInfo` classes that depend on the era or the vendor, and vendor classes with no mobile template. A `switch`
that picks a random set of `SBInfo` for each vendor is read as the union of its sets.
