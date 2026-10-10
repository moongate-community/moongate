"""``modernuo-vendors``: the shops of ModernUO's vendors into ``templates/shops/<vendor>.toml``, one file a vendor class.

Nothing is compiled or run: the C# is read as syntax. A type becomes the item template with the graphic ModernUO gives the line; a type
with no template is dropped and counted in the report. What sits under a condition is left out and counted too.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, TextIO, TypeVar

from . import csharp, tomlout
from .csharp import SourceError
from .report import ConversionReport
from .textutil import snake_case, write_text

VENDORS_FOLDER = "Vendors"
MOBILES_FOLDER = "Mobiles"
ITEMS_FOLDER = "Items"
HEALERS_FOLDER = "Healers"

# Pairs of graphics of one item that ModernUO does not declare as flippable: the one it sells, the one the templates have.
KNOWN_TWINS = {
    0x0F6B: [0x0F64],  # torch
    0x1544: [0x1543],  # skull cap
    0x0EF3: [0x0E34],  # blank scroll
    0x1409: [0x1408],  # close helm
    0x140B: [0x140A],  # helmet
    0x140F: [0x140E],  # norse helm
    0x1419: [0x1412],  # plate helm
}
SB_INFO_BASE = "SBInfo"
BUY_INFO_CLASS = "InternalBuyInfo"
SELL_INFO_CLASS = "InternalSellInfo"
INIT_METHOD = "InitSBInfo"
ANIMAL_LINE = "AnimalBuyInfo"
BEVERAGE_LINE = "BeverageBuyInfo"
GENERIC_LINE = "GenericBuyInfo"

# The templates of an armor or weapon graphic: one for each era, which are all the plain piece, then one for each material. The era the
# data sets use first is the one chosen when a single template is needed.
ERAS = ["lbr", "aos", "t2a", "tol"]

# The codes of the materials in the ids of the data sets: agapite, bronze, copper, dull copper, gold, shadow, valorite, verite.
MATERIALS = ["a", "b", "c", "d", "g", "s", "va", "ve"]

HEADER = """# What it is for:
#   The shop of one kind of vendor: what its vendors sell to a player, with the price of a piece and
#   how many they start with. Every vendor template listed in vendors uses it.
#
# Fields:
#   [[shop]]      one shop
#     id          the stable id of the shop
#     vendors     the ids of the mobile templates whose vendors use the shop (a template is in one shop)
#   [[shop.buy]]  one line the vendor sells
#     item        the id of an item template
#     price       gold for one piece, at least 1
#     amount      how many pieces the vendor starts with, at least 1
#     hue         the hue of the goods; 0 keeps the one of the item template
#     name        the name shown in the shop window; empty: the client's name of the graphic
#   [[shop.sell]] one line the vendor buys from a player
#     item        the id of an item template
#     price       gold the vendor pays for one piece, at least 1
#     (amount, hue and name are written too and ignored on sell lines)
"""

# Where the name ModernUO gives a vendor class is not the one of its template.
ALIASES = {
    "armorer": ["armourer", "m_armourer", "f_armourer"],
    "fisherman": ["fisher", "m_fisher", "f_fisher"],
    "fortuneteller": ["f_gypsyfortuneteller"],
    # ModernUO has no spinner: UOX3, whose templates these are, gives it the list of the weaver.
    "weaver": ["weaver", "m_weaver", "f_weaver", "spinner", "m_spinner", "f_spinner"],
    "waiter": ["waiter", "m_waiter", "f_waitress"],
    "wanderinghealer": ["whealer"],
    "evilwanderinghealer": ["evilwhealer"],
}

# Where the name of a ModernUO type is not the one of its template: the single crossbow bolt, not a stack of twenty.
TYPE_ALIASES = {"bolt": "crossbow_bolt"}

# The two graphics of one item, one for each way it faces: ModernUO sells it under one, a template may be named by the other.
_FLIPPABLE = re.compile(r"\[Flippable\(([^)\]]*)\)\]")
_ID_LINE = re.compile(r'^id\s*=\s*"([^"]+)"', re.MULTILINE)
_GRAPHIC = re.compile(r"^0x([0-9a-f]{4})_(.*)$")
T = TypeVar("T")


@dataclass
class BuyLine:
    type_name: str
    price: int
    amount: int
    graphic: int
    hue: int
    name: str = ""


@dataclass
class SellLine:
    type_name: str
    price: int


@dataclass
class SbInfo:
    name: str
    lines: list[BuyLine]
    sells: list[SellLine] = field(default_factory=list)


@dataclass
class Vendor:
    name: str
    sb_infos: list[str]


@dataclass
class ShopLine:
    item: str
    price: int
    amount: int = 1
    hue: int = 0
    name: str = ""


@dataclass
class Shop:
    id: str
    vendors: list[str]
    buy: list[ShopLine]
    sell: list[ShopLine]


@dataclass
class ShopIndex:
    by_graphic: dict[int, list[str]]
    graphics_of_type: dict[str, list[int]]
    by_name: dict[str, list[str]]
    twins: dict[int, list[int]] = field(default_factory=dict)
    ids: set[str] = field(default_factory=set)


# --- reading the C# ---


def read_sb_infos(source: str, path: str, report: ConversionReport) -> list[SbInfo]:
    """The ``SBInfo`` classes of a file: the lines each sells and buys. What sits under a condition is left out and counted."""
    root = _parse(source, path)
    infos: list[SbInfo] = []

    for owner in csharp.descendants(root, "class_declaration"):
        base_list = next((child for child in owner.children if child.type == "base_list"), None)

        if base_list is None or not any(csharp.text(child) == SB_INFO_BASE for child in csharp.named_children(base_list)):
            continue

        lines: list[BuyLine] = []
        buy_info = _nested(owner, BUY_INFO_CLASS)

        for constructor in csharp.members(buy_info, "constructor_declaration") if buy_info else []:
            for creation in csharp.descendants(constructor, "object_creation_expression"):
                _read_line(creation, constructor, lines, report)

        sells: list[SellLine] = []
        sell_info = _nested(owner, SELL_INFO_CLASS)

        for constructor in csharp.members(sell_info, "constructor_declaration") if sell_info else []:
            for call in csharp.descendants(constructor, "invocation_expression"):
                _read_sell(call, constructor, sells, report)

        infos.append(SbInfo(csharp.name_of(owner), lines, sells))

    return infos


def read_vendors(source: str, path: str, report: ConversionReport) -> list[Vendor]:
    """The vendor classes of a file: those with an ``InitSBInfo`` method, with the ``SBInfo`` classes they add."""
    root = _parse(source, path)
    vendors: list[Vendor] = []

    for owner in csharp.descendants(root, "class_declaration"):
        method = next((m for m in csharp.members(owner, "method_declaration") if csharp.name_of(m) == INIT_METHOD), None)

        if method is None:
            continue

        names: list[str] = []

        for creation in csharp.descendants(method, "object_creation_expression"):
            name = csharp.text(csharp.creation_type(creation))

            if not name.startswith("SB"):
                continue

            if _is_conditional(creation, method):
                report.count(f"conditional SBInfo {name}")

                continue

            names.append(name)

        vendors.append(Vendor(csharp.name_of(owner), names))

    return vendors


def _parse(source: str, path: str):
    root = csharp.parse(source)
    csharp.check(root, path)

    return root


def _nested(owner, name: str):
    return next((member for member in csharp.members(owner, "class_declaration") if csharp.name_of(member) == name), None)


def _is_conditional(node, root) -> bool:
    """Under an if, an else or a loop: it depends on the era or on the vendor. A switch picks a set at random, so the shop takes all the sets."""
    for ancestor in csharp.ancestors(node):
        if ancestor == root:
            return False

        if ancestor.type in ("if_statement", "for_statement", "foreach_statement", "while_statement"):
            return True

    return False


def _number(expression) -> int | None:
    return csharp.int_value(expression)


def _read_line(creation, constructor, lines: list[BuyLine], report: ConversionReport) -> None:
    kind = csharp.text(csharp.creation_type(creation))

    if kind not in (GENERIC_LINE, BEVERAGE_LINE, ANIMAL_LINE):
        return

    if kind == ANIMAL_LINE:
        report.count("animal line (pets are not sold yet)")

        return

    if _is_conditional(creation, constructor):
        report.count("conditional line")

        return

    arguments = [csharp.expression(argument) for argument in csharp.arguments(creation)]
    type_at = next((index for index, argument in enumerate(arguments) if argument.type == "typeof_expression"), -1)

    if type_at < 0:
        report.count("line without a type")

        return

    name = (csharp.string_value(arguments[type_at - 1]) or "") if type_at > 0 else ""
    # A beverage names its content between the type and the price.
    numbers_at = type_at + 1 + (1 if kind == BEVERAGE_LINE else 0)
    numbers = [_number(argument) for argument in arguments[numbers_at : numbers_at + 4]]

    if len(numbers) < 4 or numbers[0] is None or numbers[1] is None or numbers[2] is None:
        report.count("line with a number that is no literal")

        return

    if kind == BEVERAGE_LINE:
        report.count("beverage content dropped (the item template decides what it holds)")

    if len(arguments) > numbers_at + 4:
        report.count("constructor arguments dropped")

    if numbers[3] is None:
        report.count("hue that is no literal (taken as 0)")

    lines.append(BuyLine(csharp.text(csharp.named_children(arguments[type_at])[0]), numbers[0], numbers[1], numbers[2], numbers[3] or 0, name))


def _read_sell(call, constructor, sells: list[SellLine], report: ConversionReport) -> None:
    """``Add(typeof(BreadLoaf), 3);``"""
    target = call.children[0]
    arguments = [csharp.expression(argument) for argument in csharp.arguments(call)]

    if target.type != "identifier" or csharp.text(target) != "Add" or len(arguments) != 2 or arguments[0].type != "typeof_expression":
        return

    if _is_conditional(call, constructor):
        report.count("conditional sell line")

        return

    gold = _number(arguments[1])

    if gold is None:
        report.count("sell line with a price that is no literal")

        return

    sells.append(SellLine(csharp.text(csharp.named_children(arguments[0])[0]), gold))


# --- the templates ---


def _id_name(template_id: str) -> str:
    """The words after the graphic of an item template id: ``0x103b_bread_loaf`` is ``bread_loaf``."""
    match = _GRAPHIC.match(template_id)

    return match.group(2) if match else ""


def era_base(candidates: list[str]) -> str | None:
    """The plain piece of the first era the graphic has; None for a graphic that is not an armor or weapon family."""
    for era in ERAS:
        found = next((candidate for candidate in candidates if _id_name(candidate) == era), None)

        if found is not None:
            return found

    return None


def _is_material_family(candidates: list[str]) -> bool:
    """Templates that are all one piece in the materials of the data sets, with no plain piece among them."""
    return len(candidates) > 1 and all(_id_name(candidate) in MATERIALS for candidate in candidates)


def ids_of(folder: Path) -> set[str]:
    """The ids of the templates of a folder: the ``id = ...`` lines of its files."""
    ids: set[str] = set()

    for path in folder.rglob("*.toml"):
        ids.update(_ID_LINE.findall(path.read_bytes().decode("utf-8-sig", errors="replace")))

    return ids


def items_by_graphic(folder: Path) -> dict[int, list[str]]:
    """The item template ids of a folder by the graphic in front of them, in the order of the ids."""
    by_graphic: dict[int, list[str]] = {}

    for template_id in sorted(ids_of(folder)):
        match = _GRAPHIC.match(template_id)

        if match:
            by_graphic.setdefault(int(match.group(1), 16), []).append(template_id)

    return by_graphic


def _items_by_name(by_graphic: dict[int, list[str]]) -> dict[str, list[str]]:
    by_name: dict[str, list[str]] = {}

    for ids in by_graphic.values():
        for template_id in ids:
            by_name.setdefault(_id_name(template_id), []).append(template_id)

    return by_name


def _graphics_of_type(infos) -> dict[str, list[int]]:
    graphics: dict[str, list[int]] = {}

    for info in infos:
        for line in info.lines:
            known = graphics.setdefault(line.type_name, [])

            if line.graphic not in known:
                known.append(line.graphic)

    return graphics


def twins_of(folder: Path) -> dict[int, list[int]]:
    """The other graphics of each graphic: the known pairs and those ModernUO's items declare as flippable."""
    twins: dict[int, list[int]] = {graphic: list(others) for graphic, others in KNOWN_TWINS.items()}

    if not folder.is_dir():
        return twins

    for path in sorted(folder.rglob("*.cs")):
        for arguments in _FLIPPABLE.findall(path.read_bytes().decode("utf-8-sig", errors="replace")):
            try:
                graphics = [int(argument.strip(), 0) for argument in arguments.split(",") if argument.strip()]
            except ValueError:
                continue

            for graphic in graphics:
                others = twins.setdefault(graphic, [])
                others.extend(other for other in graphics if other != graphic and other not in others)

    return twins


def _family_of(graphic: int, index: ShopIndex) -> list[str] | None:
    """The templates of a graphic, else those of the other graphic of its flippable pair."""
    family = index.by_graphic.get(graphic)

    if family is not None:
        return family

    return next((index.by_graphic[twin] for twin in index.twins.get(graphic, []) if twin in index.by_graphic), None)


def _item_of(line: BuyLine, index: ShopIndex, report: ConversionReport) -> str | None:
    """The template of a graphic: the one named like the type, else the only one, else the first (and the report says so)."""
    # A template named exactly like the type is that item, whatever graphic pair it shares: the mapmaker's pen
    # and the scribe's pen are one pair of graphics and two items.
    if line.type_name.lower() in index.ids:
        return line.type_name.lower()

    candidates = _family_of(line.graphic, index)
    wanted = TYPE_ALIASES.get(snake_case(line.type_name), snake_case(line.type_name))

    if candidates is None:
        named_so = index.by_name.get(wanted, [])

        # A template of another graphic is the item only when it is the one template of that name.
        if len(named_so) == 1:
            report.count(f"graphic 0x{line.graphic:04x} ({line.type_name}) has no item template, took {named_so[0]} by its name")

            return named_so[0]

        report.count(f"no item template for graphic 0x{line.graphic:04x} ({line.type_name})")

        return None

    named = next((candidate for candidate in candidates if _id_name(candidate) == wanted), None)

    if named is not None:
        return named

    plain = era_base(candidates)

    if plain is not None:
        return plain

    if _is_material_family(candidates):
        # The plain piece may be a template of the other graphic of the pair, which the materials are made from.
        for twin in index.twins.get(line.graphic, []):
            plain = era_base(index.by_graphic.get(twin, []))

            if plain is not None:
                return plain

        report.count(f"graphic 0x{line.graphic:04x} ({line.type_name}) has only material templates, no plain piece: left out")

        return None

    if len(candidates) > 1:
        report.count(f"graphic 0x{line.graphic:04x} ({line.type_name}) has {len(candidates)} item templates, took {candidates[0]}")

    return candidates[0]


def _templates_of(class_name: str, mobile_ids: set[str]) -> list[str]:
    lower = class_name.lower()
    # BlacksmithGuildmaster is blacksmithguildmaster in the data of UOX3 and blacksmith_guildmaster in this server's.
    snake = snake_case(class_name)
    names = ALIASES.get(lower) or [lower, "m_" + lower, "f_" + lower, snake, "m_" + snake, "f_" + snake]

    return [name for name in names if name in mobile_ids]


def _build(vendor: Vendor, sb_infos: dict[str, SbInfo], index: ShopIndex, mobile_ids: set[str], claimed: dict[str, str], report: ConversionReport) -> Shop | None:
    shop_id = snake_case(vendor.name)
    templates = _templates_of(vendor.name, mobile_ids)

    if not vendor.sb_infos:
        report.count(f"vendor {vendor.name} adds no SBInfo")

        return None

    if not templates:
        report.count(f"vendor {vendor.name} has no mobile template")

        return None

    lines: list[ShopLine] = []
    seen: set[tuple[str, int, int]] = set()

    for name in vendor.sb_infos:
        info = sb_infos.get(name)

        if info is None:
            report.count(f"unknown SBInfo {name}")

            continue

        for line in info.lines:
            item = _item_of(line, index, report)

            if item is None:
                continue

            # Two SBInfo of one vendor may sell the same thing for the same price.
            key = (item, line.price, line.hue)

            if key not in seen:
                seen.add(key)
                lines.append(ShopLine(item, max(1, line.price), max(1, line.amount), line.hue, line.name))

    if not lines:
        report.count(f"vendor {vendor.name} sells nothing that has an item template")

        return None

    vendors: list[str] = []

    for template in templates:
        if template in claimed:
            report.count(f"template {template} is in shop {claimed[template]} already")
        else:
            claimed[template] = shop_id
            vendors.append(template)

    if not vendors:
        return None

    return Shop(shop_id, vendors, lines, _sell_lines(vendor, sb_infos, index, report))


def _sell_lines(vendor: Vendor, sb_infos: dict[str, SbInfo], index: ShopIndex, report: ConversionReport) -> list[ShopLine]:
    """What the vendor buys: the templates of the graphics its shops sell a type under, else the templates named like it."""
    lines: list[ShopLine] = []
    seen: set[str] = set()

    for name in (name for name in vendor.sb_infos if name in sb_infos):
        for sell in sb_infos[name].sells:
            items: list[str] = []

            # A vendor buys a piece whatever it is made of, so an armor or weapon graphic sells every template of it.
            for graphic in index.graphics_of_type.get(sell.type_name, []):
                family = _family_of(graphic, index)

                if family is not None and (era_base(family) is not None or _is_material_family(family)):
                    items.extend(family)
                else:
                    one = _item_of(BuyLine(sell.type_name, 0, 0, graphic, 0), index, report)

                    if one is not None:
                        items.append(one)

            if not items:
                items = index.by_name.get(snake_case(sell.type_name), [])

            if not items:
                report.count(f"no item template for the sold type {sell.type_name}")

            for item in items:
                if item not in seen:
                    seen.add(item)
                    lines.append(ShopLine(item, max(1, sell.price)))

    return lines


def _cap_sell_prices(shops: list[Shop], report: ConversionReport) -> None:
    """No vendor pays more for a piece than the lowest price any vendor asks for it: buying from one and selling to another is never a profit."""
    lowest: dict[str, int] = {}

    for shop in shops:
        for line in shop.buy:
            lowest[line.item] = min(line.price, lowest.get(line.item, 2**31 - 1))

    for shop in shops:
        for line in shop.sell:
            asked = lowest.get(line.item)

            if asked is not None and line.price > asked:
                report.count(f"sell price of {line.item} lowered to {asked}, the lowest price it is sold at")
                line.price = asked


def _read_all(folder: Path, read: Callable[[str, str], list[T]]) -> list[T]:
    found: list[T] = []

    for path in sorted((path for path in folder.rglob("*.cs") if path.is_file()), key=str):
        found.extend(read(csharp.read_source(path), str(path)))

    return found


# --- writing ---


def serialize(shop: Shop) -> str:
    """One shop as the server's serializer writes it."""
    lines = ["[[shop]]", f"id = {tomlout.basic(shop.id)}", f"vendors = {tomlout.strings(shop.vendors)}"]

    if not shop.buy:
        lines.append("buy = []")

    if not shop.sell:
        lines.append("sell = []")

    for name, shop_lines in (("buy", shop.buy), ("sell", shop.sell)):
        for position, line in enumerate(shop_lines):
            if position:
                lines.append("")

            lines += [
                f"[[shop.{name}]]",
                f"item = {tomlout.basic(line.item)}",
                f"price = {line.price}",
                f"amount = {line.amount}",
                f"hue = {line.hue}",
                f"name = {tomlout.basic(line.name)}",
            ]

    return "\n".join(lines) + "\n"


# --- the run ---


def run(source: Path, items: Path, mobiles: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    root = source / MOBILES_FOLDER / VENDORS_FOLDER if (source / MOBILES_FOLDER / VENDORS_FOLDER).is_dir() else source

    if not (root / "SBInfo").is_dir() or not (root / "NPC").is_dir():
        error.write(f"{source}: it must hold SBInfo and NPC folders, or Mobiles/Vendors with them.\n")

        return 2

    if not items.is_dir() or not mobiles.is_dir():
        error.write(f"The item or mobile templates folder does not exist: {items}, {mobiles}\n")

        return 2

    try:
        report = ConversionReport()
        sb_infos: dict[str, SbInfo] = {}

        for info in _read_all(root / "SBInfo", lambda text, path: read_sb_infos(text, path, report)):
            if info.name in sb_infos:
                report.count(f"SBInfo {info.name} is defined twice (the first is used)")
            else:
                sb_infos[info.name] = info

        vendors = _read_all(root / "NPC", lambda text, path: read_vendors(text, path, report))

        # The healers sell too, and are not among the vendors: they stand in a folder beside them.
        if (root.parent / HEALERS_FOLDER).is_dir():
            vendors += _read_all(root.parent / HEALERS_FOLDER, lambda text, path: read_vendors(text, path, report))

        by_graphic = items_by_graphic(items)
        index = ShopIndex(by_graphic, _graphics_of_type(sb_infos.values()), _items_by_name(by_graphic), twins_of(source / ITEMS_FOLDER), ids_of(items))
        mobile_ids = ids_of(mobiles)
        claimed: dict[str, str] = {}
        files: list[tuple[Path, Shop]] = []

        for vendor in sorted(vendors, key=lambda vendor: vendor.name):
            shop = _build(vendor, sb_infos, index, mobile_ids, claimed, report)

            if shop is not None:
                files.append((destination / f"{shop.id}.toml", shop))

        _cap_sell_prices([shop for _, shop in files], report)

        if not files:
            report.write(output)
            error.write(f"{source}: no vendor with a shop and a mobile template.\n")

            return 2

        for path, shop in files:
            write_text(path, HEADER + serialize(shop))
            output.write(f"shops/{path.name} ({len(shop.buy)} lines, {len(shop.vendors)} vendors)\n")

        report.write(output)
        output.write(f"Wrote {len(files)} shop(s), {sum(len(shop.buy) for _, shop in files)} lines from ModernUO's vendors.\n")

        return 0
    except (SourceError, OSError, UnicodeError) as exception:
        error.write(f"Vendor conversion failed: {exception}\n")

        return 2
