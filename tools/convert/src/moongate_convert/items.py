"""UOX3's item blocks and loot lists as the server's item and loot templates, and the TOML they are written as.

An item block with an ``id=`` of its own is a template whose id comes from its header (and its ``name=`` for a bare hex header); a block
with none but one parent that converted is a template too, with the header for id. A ``[LOOTLIST n]`` block is a loot table.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field, replace
from decimal import Decimal

from . import tomlout
from .dfn import DfnBlock, IgnoreCaseDict, IgnoreCaseSet, get_targets, parent_targets, uox_number
from .item_index import ItemIndex  # noqa: F401  (the index the mobile pass is given)
from .specs import HueSpec, RangeValue
from .textutil import snake_case, trim, try_int
from .uox_data import WEAPON_TYPE_BY_GRAPHIC, ScriptAssociations

FOOD_TYPE = 14
DRINK_TYPE = 105
DYES_TYPE = 208
AXE_TYPE = 216
SHIELD_TYPE = 107
DYE_TUB_GRAPHIC = 0x0FAB
# The four piles of ore: UOX3 types them by their graphic, in itemtypes.dfn, not in their block.
ORE_GRAPHICS = (0x19B7, 0x19B8, 0x19B9, 0x19BA)
# What a carpenter works wood with: the chisels, dovetail saws, planes, saws, draw knife, froe and inshave, not the nails.
CARPENTRY_TOOL_GRAPHICS = frozenset(
    {0x1026, 0x1027, 0x1028, 0x1029, 0x102C, 0x102D, 0x1030, 0x1031, 0x1032, 0x1033, 0x1034, 0x1035, 0x10E4, 0x10E5, 0x10E6}
)
# What a tinker works with: the tinker's tools and the tool kits.
TINKERING_TOOL_GRAPHICS = frozenset({0x1EB8, 0x1EB9, 0x1EBA, 0x1EBB, 0x1EBC})
# What a tailor sews with: the sewing kit, not the scissors.
TAILORING_TOOL_GRAPHICS = frozenset({0x0F9D})
# What a cartographer draws with: the pens and ink (inscription will share them).
CARTOGRAPHY_TOOL_GRAPHICS = frozenset({0x0FBF, 0x0FC0})
# What a cook works with: the skillets, the flour sifter and the rolling pin.
COOKING_TOOL_GRAPHICS = frozenset({0x097F, 0x09E2, 0x103E, 0x1043})
# What a bowyer works with: the fletcher's tools.
FLETCHING_TOOL_GRAPHICS = frozenset({0x1022, 0x1023})
# What a smith forges with at an anvil: the smith's hammers, the sledge hammers and the tongs.
SMITHING_TOOL_GRAPHICS = frozenset({0x13E3, 0x13E4, 0x0FB4, 0x0FB5, 0x0FBB, 0x0FBC})
TWO_HANDED = 2
MAX_LAYER = 29

# What UOX3 files under drinks and nobody drinks: an ingredient.
NOT_DRUNK = {"0x09ec_jar_of_honey"}

# What UOX3 files under food and nobody eats as it is: an ingredient, and the fish that ModernUO gives a spell.
NOT_EATEN = {"0x0a1e_bowl_of_flour", "base_magic_fish"}

# The LayerType names of the server, by value (1 to 29; 0 is none).
LAYERS = [
    "OneHanded", "TwoHanded", "Shoes", "Pants", "Shirt", "Helm", "Gloves", "Ring", "Talisman", "Neck", "Hair", "Waist", "InnerTorso",
    "Bracelet", "Face", "FacialHair", "MiddleTorso", "Earrings", "Arms", "Cloak", "Backpack", "OuterTorso", "OuterLegs", "InnerLegs", "Mount",
    "ShopBuy", "ShopResale", "ShopSell", "Bank",
]


@dataclass
class ItemTemplate:
    """An ``ItemTemplate`` of the server, with the fields in the order the server's serializer writes them."""

    id: str
    item_id: int
    base_id: str | None = None
    name: str | None = None
    script_id: str | None = None
    movable: bool | None = None
    weight: Decimal | None = None
    amount: RangeValue | None = None
    stackable: bool | None = None
    layer: str | None = None
    two_handed_weapon: bool | None = None
    weapon_type: str | None = None
    damage_min: int | None = None
    damage_max: int | None = None
    speed: int | None = None
    strength_required: int | None = None
    armor_rating: int | None = None
    max_hits: int | None = None
    dyeable: bool | None = None
    buy_price: int | None = None
    sell_price: int | None = None
    decays: bool | None = None
    loot_type: str | None = None
    tags: dict[str, str] | None = None
    visibility: str | None = None
    hue: HueSpec | None = None
    max_weight: int | None = None


@dataclass
class LootEntry:
    weight: int = 1
    item_id: str | None = None
    loot_template_id: str | None = None
    comment: str | None = None
    amount: RangeValue = field(default_factory=lambda: RangeValue.from_value(1))


@dataclass
class LootTemplate:
    id: str
    entries: list[LootEntry] = field(default_factory=list)


# --- ids ---


def is_bare_hex(header: str) -> bool:
    return header[:2].lower() == "0x"


def try_compute_id(block: DfnBlock) -> tuple[str, int] | None:
    """A block's id and graphic from the block alone; None for a block with no ``id=`` of its own (a ``get=a b`` alias, or a block that is no
    item). UOX3 picks one id of a list (``id=0x0c4f 0x0c50``) at random; a template has one graphic, so the first is kept."""
    text = block.fields.get("id")
    graphic = uox_number(text) if text is not None else None

    if graphic is None or graphic < 0:
        return None

    # The header alone is always unique (a duplicate is caught while every block is read). name= is not: UOX3 reuses it across many facing,
    # material or damage-state variants, sometimes literally "#", so it only ever adds to the header of a bare hex block.
    name = block.fields.get("name")
    combined = f"{block.header}_{name}" if is_bare_hex(block.header) and name else block.header

    return snake_case(combined), graphic


def single_parent(block: DfnBlock) -> str | None:
    """The one parent a block inherits from; None for none or for a random ``get=a b``."""
    targets = parent_targets(block)

    return targets[0] if len(targets) == 1 else None


# --- flattening ---


def flatten(block: DfnBlock, blocks_by_header: IgnoreCaseDict[DfnBlock]) -> DfnBlock:
    """Inlines a ``get=`` target that has no ``id=`` of its own, such as ``[base_coin]``, into the block that names it. UOX3 applies such a
    target's lines in place, so its fields (a coin's ``weight=2``, ``pileable=1``) would otherwise never reach the templates made from its
    children. A target with an ``id=`` is converted on its own and stays a base id."""
    return _flatten(block, blocks_by_header, IgnoreCaseSet())


def _flatten(block: DfnBlock, blocks_by_header: IgnoreCaseDict[DfnBlock], visiting: IgnoreCaseSet) -> DfnBlock:
    get_text = block.fields.get("get")

    # get=a b is a random pick among aliases, not a parent: it is left to the builder.
    if get_text is None:
        return block

    parts = [part for part in get_text.split(" ") if part]

    if len(parts) != 1:
        return block

    parent = blocks_by_header.get(parts[0])

    if parent is None or try_compute_id(parent) is not None or block.header in visiting:
        return block

    visiting.add(block.header)
    flat_parent = _flatten(parent, blocks_by_header, visiting)
    visiting.discard(block.header)

    # The child's own lines win, as they come after get= in UOX3. The child's get= is replaced by whatever its parent resolved to, so an id=
    # ancestor further up still becomes the base id.
    fields = flat_parent.fields.copy()
    fields.pop("get", None)

    for key, value in block.fields.items():
        if key.lower() != "get":
            fields[key] = value

    parent_get = flat_parent.fields.get("get")

    if parent_get is not None:
        fields["get"] = parent_get

    entries = [line for line in flat_parent.entries if not _is_get_line(line)] + [line for line in block.entries if not _is_get_line(line)]

    return replace(block, fields=fields, entries=entries)


def _is_get_line(line: str) -> bool:
    separator = line.find("=")

    return separator >= 0 and trim(line[:separator]).lower() == "get"


# --- items ---


def build_item(block: DfnBlock, id_by_header: IgnoreCaseDict[str], scripts: ScriptAssociations | None = None) -> ItemTemplate | None:
    """The template of a block, its ``get=`` target resolved against the precomputed header-to-id map; None for a block that has no id there."""
    item_id = id_by_header.get(block.header)

    if item_id is None:
        return None

    computed = try_compute_id(block)
    graphic = computed[1] if computed is not None else 0
    template = ItemTemplate(id=item_id, item_id=graphic, movable=_movable(block))
    _apply_base_fields(block, template)
    fields = block.fields

    script_id = scripts.script_id_for(block, graphic) if scripts is not None else None
    kind = _number(fields.get("TYPE"))

    # UOX3 gives its blade script by graphic, to a flower garland among them: only a weapon cuts.
    if script_id == "blade" and graphic not in WEAPON_TYPE_BY_GRAPHIC:
        script_id = None

    if script_id is not None:
        template.script_id = script_id
    elif item_id not in NOT_EATEN and kind == FOOD_TYPE:
        # What UOX3 lets a player eat: scripts/items/food.lua.
        template.script_id = "food"
    elif item_id not in NOT_DRUNK and kind == DRINK_TYPE:
        # What UOX3 lets a player drink: scripts/items/drink.lua, in place of UOX3's own pitchers.js.
        template.script_id = "drink"
    elif kind == AXE_TYPE:
        # What UOX3 chops a tree with: scripts/items/axe.lua.
        template.script_id = "axe"
    elif graphic in SMITHING_TOOL_GRAPHICS and "prospector" not in item_id.lower():
        # The prospector's tool shares the sledge hammer's graphic but digs: no forging with it.
        # What UOX3's crafting tool script opens blacksmithing with: scripts/items/smithing_tool.lua.
        template.script_id = "smithing_tool"
    elif graphic in TINKERING_TOOL_GRAPHICS and "taxiderm" not in item_id.lower():
        # The taxidermy kit shares a tool kit's graphic but stuffs trophies: no tinkering with it.
        # What UOX3's crafting tool script opens tinkering with: scripts/items/tinkering_tool.lua.
        template.script_id = "tinkering_tool"
    elif graphic in CARTOGRAPHY_TOOL_GRAPHICS:
        # What a cartographer draws with: scripts/items/cartography_tool.lua.
        template.script_id = "cartography_tool"
    elif graphic in COOKING_TOOL_GRAPHICS:
        # What a cook works with: scripts/items/cooking_tool.lua.
        template.script_id = "cooking_tool"
    elif graphic in FLETCHING_TOOL_GRAPHICS:
        # What a bowyer works with: scripts/items/fletching_tool.lua.
        template.script_id = "fletching_tool"
    elif graphic in TAILORING_TOOL_GRAPHICS:
        # What UOX3's crafting tool script opens tailoring with: scripts/items/tailoring_tool.lua.
        template.script_id = "tailoring_tool"
    elif graphic in CARPENTRY_TOOL_GRAPHICS:
        # What UOX3's crafting tool script opens carpentry with: scripts/items/carpentry_tool.lua.
        template.script_id = "carpentry_tool"
    elif kind == DYES_TYPE:
        # UOX3's dyes are hard-coded: scripts/items/dyes.lua.
        template.script_id = "dyes"
    elif is_bare_hex(block.header) and graphic in ORE_GRAPHICS:
        # What UOX3 smelts at a forge: scripts/items/ore.lua. A named pile of another metal has no rule yet.
        template.script_id = "ore"
    elif is_bare_hex(block.header) and graphic == DYE_TUB_GRAPHIC:
        # UOX3 types the tub by its graphic, in itemtypes.dfn, not in its block: scripts/items/dye_tub.lua.
        template.script_id = "dye_tub"

    # A torch has the layer of a two handed weapon and no dir=: the light script is what says it is a light.
    if template.script_id == "light":
        template.two_handed_weapon = None

    # dyeable= and dye= are the same tag in UOX3; 0 takes it away from what a base gave.
    dyeable = _number(fields.get("dyeable") if "dyeable" in fields else fields.get("dye"))

    if dyeable is not None:
        template.dyeable = dyeable != 0

    # UOX3's visible= is 0 for everyone; 1 (hidden), 2 (magically invisible) and 3 (GM hidden) all keep the item from players, the closest
    # being visible to staff only. 0 is written out so it overrides a hidden parent such as base_spawner.
    visible = _number(fields.get("visible"))

    if visible is not None:
        template.visibility = "game_master" if 1 <= visible <= 3 else "regular"

    name = fields.get("name")

    if name:
        template.name = name

    # What a player drinks: scripts/items/potion.lua.
    if template.script_id is None and item_id.lower() in POTION_TEMPLATES:
        template.script_id = "potion"

    # What a player throws: scripts/items/explosion_potion.lua.
    if template.script_id is None and item_id.lower() in EXPLOSION_POTIONS:
        template.script_id = "explosion_potion"

    # A blank map says it is blank when it is opened: scripts/items/map_item.lua.
    if template.script_id is None and (template.name or "").lower() == BLANK_MAP:
        template.script_id = "map_item"

    # UOX3 reads COLOR and COLOUR as one tag.
    hue = HueSpec.try_parse(fields.get("color") if "color" in fields else fields.get("colour"))

    if hue is not None:
        template.hue = hue

    weight_max = _number(fields.get("weightmax"))

    if weight_max is not None:
        # Hundredths of a stone, as weight=: weightmax=40000 is 400 stones. Whole stones here, rounded up.
        template.max_weight = -(-weight_max // 100)

    # Only single-parent inheritance maps onto the base id. get=a b names an alias, not a parent; an unresolved single target (never
    # converted) is dropped like any other field this converter cannot carry over. A block that gets itself inherits nothing.
    parent = single_parent(block)

    if parent is not None:
        base_id = id_by_header.get(parent)

        if base_id is not None and base_id != item_id:
            template.base_id = base_id

    return template


def _number(text: str | None) -> int | None:
    return uox_number(text) if text is not None else None


def _movable(block: DfnBlock) -> bool | None:
    """UOX3 movable= is 0 for the client default, 1 always, 2 never and 3 owner only; the default stays unset so the template follows tiledata."""
    text = block.fields.get("movable")

    if text in ("1", "3"):
        return True

    return False if text == "2" else None


def _apply_base_fields(block: DfnBlock, template: ItemTemplate) -> None:
    fields = block.fields

    # UOX3 weighs in hundredths of a stone: weight=700 is 7 stones, a coin's weight=2 is 0.02.
    hundredths = _number(fields.get("weight"))

    if hundredths is not None:
        template.weight = Decimal(hundredths) / Decimal(100)

    amount = _number(fields.get("amount"))

    if amount is not None and amount >= 1:
        template.amount = RangeValue.from_value(amount)

    pileable = _number(fields.get("pileable"))

    if pileable is not None:
        template.stackable = pileable != 0

    layer = _number(fields.get("layer"))

    if layer is not None and 1 <= layer <= MAX_LAYER:
        template.layer = snake_case(LAYERS[layer - 1])

    # As UOX3 decides at equip time: layer 2 takes both hands unless the item is a shield (type=107) or a light (dir=, a torch or lantern),
    # which go in the other hand.
    if layer == TWO_HANDED and template.layer is not None and not _is_shield(block) and not _is_light(block):
        template.two_handed_weapon = True

    # value=buy sell; one number sets both.
    value_text = fields.get("value")

    if value_text is not None:
        prices = [price for price in value_text.split(" ") if price]
        buy = uox_number(prices[0]) if prices else None

        if buy is not None:
            template.buy_price = buy
            sell = uox_number(prices[1]) if len(prices) >= 2 else None
            template.sell_price = sell if sell is not None else buy

    decay = _number(fields.get("decay"))

    if decay is not None:
        template.decays = decay != 0

    # newbie is usually a bare flag line, sometimes newbie=1.
    if any(trim(line).lower() == "newbie" for line in block.entries) or fields.get("newbie") == "1":
        template.loot_type = "newbied"

    _apply_combat_fields(block, template)
    _apply_tags(block, template)
    _apply_map_preset(template)


def _apply_combat_fields(block: DfnBlock, template: ItemTemplate) -> None:
    """What combat reads, as UOX3 keeps it: damage=min max (one number is both), spd, str, def and hp=min max, of which the most is the
    durability. A kind of weapon follows the graphic of the block, by UOX3's own table, so it goes on the item that has the id= and its eras
    inherit it."""
    fields = block.fields
    damage = _read_range(fields.get("damage")) if "damage" in fields else None

    if damage is not None and damage[1] > 0:
        template.damage_min, template.damage_max = damage
    else:
        high = _number(fields.get("hidamage"))

        # UOX3 also reads the two ends apart, lodamage and hidamage: the practice weapons are written so.
        if high is not None and high > 0:
            low = _number(fields.get("lodamage"))
            template.damage_max = high
            template.damage_min = low if low is not None and 0 <= low <= high else high

    # spd, or speed which UOX3 reads as the same tag.
    for tag in ("spd", "speed"):
        speed = _number(fields.get(tag))

        if speed is not None and speed > 0:
            template.speed = speed

            break

    strength = _number(fields.get("str"))

    if strength is not None and strength > 0:
        template.strength_required = strength

    armor = _number(fields.get("def"))

    if armor is not None and armor > 0:
        template.armor_rating = armor

    hits = _read_range(fields.get("hp")) if "hp" in fields else None

    if hits is not None and hits[1] > 0:
        template.max_hits = hits[1]

    graphic = _number(fields.get("id"))

    if graphic is not None and graphic in WEAPON_TYPE_BY_GRAPHIC:
        template.weapon_type = WEAPON_TYPE_BY_GRAPHIC[graphic]


def _read_range(text: str | None) -> tuple[int, int] | None:
    """``5 33`` is from 5 to 33, ``3`` is 3 to 3; a value that is not numbers, or is below 0, is not read."""
    if text is None:
        return None

    parts = [part for part in text.split(" ") if part]

    if not 1 <= len(parts) <= 2:
        return None

    low = uox_number(parts[0])

    if low is None or low < 0:
        return None

    if len(parts) == 1:
        return low, low

    high = uox_number(parts[1])

    return (low, high) if high is not None and high >= low else None


def _is_shield(block: DfnBlock) -> bool:
    return _number(block.fields.get("type")) == SHIELD_TYPE


def _is_light(block: DfnBlock) -> bool:
    direction = _number(block.fields.get("dir"))

    return direction is not None and direction != 0


def _apply_tags(block: DfnBlock, template: ItemTemplate) -> None:
    """``custominttag=name value`` and ``customstringtag=name text`` can repeat, so they are read from every line."""
    for line in block.entries:
        separator = line.find("=")

        if separator < 0:
            continue

        key = trim(line[:separator])

        if key.lower() not in ("custominttag", "customstringtag"):
            continue

        parts = [trim(part) for part in trim(line[separator + 1 :]).split(" ", 1)]

        if len(parts) == 2 and parts[0]:
            if template.tags is None:
                template.tags = {}

            template.tags[parts[0]] = parts[1]


# UOX3's preset maps by their Map tag: width, height, then the north-west and south-east corners, then the facet. The tag
# 50 is a crafted map, drawn by its cartographer.
PRESET_MAPS: dict[str, tuple[int, int, int, int, int, int, int]] = {
    "1": (200, 200, 0, 0, 5119, 4095, 0),
    "2": (400, 400, 0, 0, 5119, 4095, 0),
    "3": (200, 200, 1092, 1396, 1736, 1924, 0),
    "4": (200, 200, 256, 1792, 1736, 2560, 0),
    "5": (200, 200, 1024, 1280, 2304, 3072, 0),
    "6": (200, 200, 2500, 1900, 3000, 2400, 0),
    "7": (200, 200, 2560, 1792, 3840, 2560, 0),
    "8": (200, 200, 2560, 1792, 3840, 3072, 0),
    "9": (200, 200, 1088, 3572, 1528, 4056, 0),
    "10": (200, 200, 3530, 2022, 3818, 2298, 0),
    "11": (200, 200, 3328, 1792, 3840, 2304, 0),
    "12": (200, 200, 2360, 356, 2706, 702, 0),
    "13": (200, 200, 0, 256, 2304, 3072, 0),
    "14": (200, 200, 2467, 572, 2878, 746, 0),
    "15": (200, 200, 4156, 808, 4732, 1528, 0),
    "16": (200, 200, 3328, 768, 4864, 1536, 0),
    "17": (200, 200, 3446, 1030, 3832, 1424, 0),
    "18": (200, 200, 3328, 1024, 3840, 2304, 0),
    "19": (200, 200, 3582, 2456, 3770, 2742, 0),
    "20": (200, 200, 2714, 3329, 3100, 3639, 0),
    "21": (200, 200, 2560, 2560, 3840, 3840, 0),
    "22": (200, 200, 524, 2064, 960, 2452, 0),
    "23": (200, 200, 1792, 2630, 2118, 2952, 0),
    "24": (200, 200, 1792, 1792, 3072, 3072, 0),
    "25": (200, 200, 256, 1792, 2304, 4095, 0),
    "26": (200, 200, 2636, 592, 3064, 1012, 0),
    "27": (200, 200, 2636, 592, 3840, 1536, 0),
    "28": (200, 200, 236, 741, 766, 1269, 0),
    "29": (200, 200, 0, 512, 1792, 2048, 0),
    "30": (400, 400, 0, 0, 1448, 1430, 4),
    "31": (400, 400, 520, 0, 2580, 2050, 3),
    "32": (400, 400, 130, 136, 1927, 1468, 2),
    "33": (400, 400, 260, 2780, 1280, 4090, 5),
}
CRAFTED_MAP = "50"
BLANK_MAP = "blank map"
# The potions scripts/items/potion.lua knows: heal, refresh, strength, agility, night sight, poison and cure.
POTION_TEMPLATES = frozenset(
    {
        "lesserhealpotion", "healpotion", "greaterhealpotion", "refreshmentpotion", "totalrefreshmentpotion",
        "strengthpotion", "greaterstrengthpotion", "agilitypotion", "greateragilitypotion", "nightsightpotion",
        "lesserpoisonpotion", "poisonpotion", "greaterpoisonpotion", "deadlypoisonpotion", "lessercurepotion",
        "curepotion", "greatercurepotion",
    }
)

# The potions a player throws, scripts/items/explosion_potion.lua.
EXPLOSION_POTIONS = frozenset({"lesserexplosionpotion", "explosionpotion", "greaterexplosionpotion"})
PRESET_MAP_FIELDS = ("map_width", "map_height", "map_x1", "map_y1", "map_x2", "map_y2", "map_facet")


def _apply_map_preset(template: ItemTemplate) -> None:
    """A map with UOX3's Map tag opens with scripts/items/map_item.lua; a preset one carries its area as tags."""
    tag = (template.tags or {}).get("Map")

    if tag is None or (tag not in PRESET_MAPS and tag != CRAFTED_MAP):
        return

    template.script_id = "map_item"

    for field, value in zip(PRESET_MAP_FIELDS, PRESET_MAPS.get(tag, ())):
        template.tags[field] = str(value)


# --- loot ---

LOOT_PREFIX = "LOOTLIST "
NESTED_LOOT_PREFIX = "LOOTLIST="
NESTED_ITEM_LIST_PREFIX = "ITEMLIST="


def try_get_loot_id(header: str) -> str | None:
    """The loot table id of a ``[LOOTLIST n]`` header; None for any other header."""
    if header[: len(LOOT_PREFIX)].lower() != LOOT_PREFIX.lower():
        return None

    return snake_case(trim(header[len(LOOT_PREFIX) :]))


def build_loot(
    block: DfnBlock,
    loot_id: str,
    id_by_header: IgnoreCaseDict[str],
    item_name_by_id: dict[str, str],
    known_loot_ids: IgnoreCaseSet,
) -> tuple[LootTemplate, int]:
    """The loot table of a ``[LOOTLIST]`` block and how many of its entries point at nothing this converter could resolve."""
    entries: list[LootEntry] = []
    skipped = 0

    for raw_line in block.entries:
        entry = _parse_entry(raw_line, id_by_header, item_name_by_id, known_loot_ids)

        if entry is None:
            skipped += 1
        else:
            entries.append(entry)

    return LootTemplate(loot_id, entries), skipped


def _parse_entry(
    raw_line: str, id_by_header: IgnoreCaseDict[str], item_name_by_id: dict[str, str], known_loot_ids: IgnoreCaseSet
) -> LootEntry | None:
    weight = 1
    rest = raw_line
    pipe = raw_line.find("|")

    if pipe >= 0:
        parsed = try_int(trim(raw_line[:pipe]))
        weight = 1 if parsed is None else parsed
        rest = trim(raw_line[pipe + 1 :])

    reference = rest
    amount_text: str | None = None
    comma = rest.find(",")

    if comma >= 0:
        reference = trim(rest[:comma])
        amount_text = trim(rest[comma + 1 :])

    amount = _parse_amount(amount_text)

    if reference.lower() == "blank":
        return LootEntry(weight=weight, amount=amount)

    if reference[: len(NESTED_LOOT_PREFIX)].lower() == NESTED_LOOT_PREFIX.lower():
        nested_id = snake_case(trim(reference[len(NESTED_LOOT_PREFIX) :]))

        return LootEntry(weight=weight, loot_template_id=nested_id, amount=amount) if nested_id in known_loot_ids else None

    # ITEMLIST=, UOX3's "spawn everything in this list" sibling to LOOTLIST=, has no home in a loot entry: it is a different mechanic (spawn
    # every entry, not pick one), and never appears in real lootlists.dfn data.
    if reference[: len(NESTED_ITEM_LIST_PREFIX)].lower() == NESTED_ITEM_LIST_PREFIX.lower():
        return None

    item_id = id_by_header.get(reference)

    if item_id is None:
        return None

    return LootEntry(weight=weight, item_id=item_id, comment=item_name_by_id.get(item_id), amount=amount)


def _parse_amount(text: str | None) -> RangeValue:
    if not text:
        return RangeValue.from_value(1)

    parts = [part for part in text.split(" ") if part]

    if len(parts) >= 2:
        low, high = try_int(parts[0]), try_int(parts[1])

        if low is not None and high is not None:
            return RangeValue.from_range(low, high)

    value = try_int(parts[0])

    return RangeValue.from_value(1 if value is None else value)


# --- TOML ---

_BARE_KEY = re.compile(r"[A-Za-z0-9_-]+")


def _decimal(value: Decimal) -> str:
    text = format(value.normalize(), "f")

    return text if "." in text else text + ".0"


def _key(name: str) -> str:
    return name if _BARE_KEY.fullmatch(name) else tomlout.basic(name)


def _bool(value: bool) -> str:
    return "true" if value else "false"


def serialize_items(items: list[ItemTemplate]) -> str:
    """The ``[[item]]`` tables of a file, in the order the server's serializer writes the fields of a template."""
    blocks: list[str] = []

    for item in items:
        lines = [f"id = {tomlout.basic(item.id)}"]

        if item.base_id is not None:
            lines.append(f"base_id = {tomlout.basic(item.base_id)}")

        lines.append(f"item_id = {item.item_id}")

        if item.name is not None:
            lines.append(f"name = {tomlout.basic(item.name)}")

        lines.append('rarity = "common"')

        if item.script_id is not None:
            lines.append(f"script_id = {tomlout.basic(item.script_id)}")

        optional: list[tuple[str, object]] = [
            ("movable", item.movable),
            ("weight", item.weight),
            ("amount", item.amount),
            ("stackable", item.stackable),
            ("layer", item.layer),
            ("two_handed_weapon", item.two_handed_weapon),
            ("weapon_type", item.weapon_type),
            ("damage_min", item.damage_min),
            ("damage_max", item.damage_max),
            ("speed", item.speed),
            ("strength_required", item.strength_required),
            ("armor_rating", item.armor_rating),
            ("max_hits", item.max_hits),
            ("dyeable", item.dyeable),
            ("buy_price", item.buy_price),
            ("sell_price", item.sell_price),
            ("decays", item.decays),
            ("loot_type", item.loot_type),
            ("visibility", item.visibility),
            ("hue", item.hue),
            ("max_weight", item.max_weight),
        ]

        for name, value in optional:
            if value is not None:
                lines.append(f"{name} = {_value(value)}")

        # A table comes after the plain keys of its template, whatever the order of the fields.
        if item.tags:
            lines.append("[item.tags]")
            lines += [f"{_key(key)} = {tomlout.basic(text)}" for key, text in item.tags.items()]

        blocks.append("[[item]]\n" + "\n".join(lines) + "\n")

    return "\n".join(blocks)


def _value(value: object) -> str:
    if isinstance(value, bool):
        return _bool(value)

    if isinstance(value, Decimal):
        return _decimal(value)

    if isinstance(value, (HueSpec, RangeValue)):
        return value.to_toml()

    if isinstance(value, str):
        return tomlout.basic(value)

    return str(value)


def serialize_loot(loot: LootTemplate) -> str:
    """The ``[[loot]]`` table of a loot table file."""
    lines = ["[[loot]]", f"id = {tomlout.basic(loot.id)}"]

    if not loot.entries:
        lines.append("entries = []")

    text = "\n".join(lines) + "\n"
    entries: list[str] = []

    for entry in loot.entries:
        entry_lines = ["[[loot.entries]]", f"weight = {entry.weight}"]

        if entry.item_id is not None:
            entry_lines.append(f"item_id = {tomlout.basic(entry.item_id)}")

        if entry.loot_template_id is not None:
            entry_lines.append(f"loot_template_id = {tomlout.basic(entry.loot_template_id)}")

        if entry.comment is not None:
            entry_lines.append(f"comment = {tomlout.basic(entry.comment)}")

        entry_lines.append(f"amount = {entry.amount.to_toml()}")
        entries.append("\n".join(entry_lines) + "\n")

    return text + "\n".join(entries)
