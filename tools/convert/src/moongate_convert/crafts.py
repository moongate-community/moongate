"""UOX3's create menus (``dfndata/create``) into the crafts of Moongate (``data/crafts``): one file a craft, with its groups and
recipes, and ``resources.toml`` with the lists of templates a recipe's resource may name."""

from __future__ import annotations

import os
import re
import tomllib
from pathlib import Path
from typing import TextIO

from . import dfn, tomlout
from .textutil import read_lines, trim

# The crafts this converter knows: the UOX3 file name -> (the craft's id, the name shown, the skill, the root submenu).
CRAFTS: dict[str, tuple[str, str, str, int]] = {
    "carpentry": ("carpentry", "Carpentry", "carpentry", 19),
    "smithing": ("blacksmithing", "Blacksmithing", "blacksmithy", 1),
    "tailoring": ("tailoring", "Tailoring", "tailoring", 39),
    "tinkering": ("tinkering", "Tinkering", "tinkering", 59),
    "bowcraft": ("fletching", "Bowcraft and Fletching", "bowcraft_fletching", 49),
    "cooking": ("cooking", "Cooking", "cooking", 750),
    "cartography": ("cartography", "Cartography", "cartography", 80),
    "alchemy": ("alchemy", "Alchemy", "alchemy", 89),
}

# Inscription is built from data/spells.toml, not from a UOX3 menu: the craft's id, the name shown, the skill and the sound.
INSCRIPTION = ("inscription", "Inscription", "inscription", 0x0249)

# The circles of Magery, which are the groups of its gump.
CIRCLE_NAMES = ["First Circle", "Second Circle", "Third Circle", "Fourth Circle", "Fifth Circle", "Sixth Circle", "Seventh Circle", "Eighth Circle"]

# What a circle asks of a scribe, by the classic tables: the least of Inscription to try it (a first circle scroll is always
# tried) and the mana. A window is fifty points long.
INSCRIPTION_SKILL = [-25.0, -10.8, 3.5, 17.8, 32.1, 46.4, 60.7, 75.0]
INSCRIPTION_WINDOW = 50.0
INSCRIPTION_MANA = [4, 6, 9, 11, 14, 20, 40, 50]

# The list of what a scribe writes on, and the template in it.
BLANK_SCROLLS = ("blank_scrolls", ["0x0e34_a_blank_scroll"])

# The sound UOX3 plays for every recipe of a craft, by the craft's id.
SOUNDS: dict[str, int] = {"carpentry": 0x023D, "blacksmithing": 0x002A, "tailoring": 0x0248, "tinkering": 0x023B, "fletching": 0x0055, "cooking": 0x0057, "cartography": 0x0249, "alchemy": 0x0242}

# The groups that make deeds, which mean nothing until houses exist.
SKIPPED_GROUPS = {"house additions", "blacksmith add-ons", "tailor add-ons", "cooking add-ons", "traps"}

# What a player already gets elsewhere: boards from an axe, kindling hacked off a tree with a blade.
SKIPPED_ITEMS = {"0x1bd7", "0x0de1"}

# The crafts whose root menu holds recipes of its own: the name of the group they form, first.
ROOT_GROUPS = {"fletching": "Weapons", "cartography": "Maps"}

# The menus UOX3 opens from a second tool (arrows and bolts from the fletching tool), walked after the root.
EXTRA_ROOTS = {"fletching": ["51"]}

NAME_FIXES = {"Magincian Throne": "Magician Throne", "one shaft": "Shaft", "one arrow": "Arrow", "one bolt": "Bolt"}

# The main skill lines UOX3 gets wrong, by the craft's skill and the recipe's name (lower case): the whole SKILL= value.
SKILL_FIXES = {
    ("tinkering", "scales"): "37 638 1140",
    ("tinkering", "heating stand"): "37 643 1140",
    # UOX3's cartography is off by a digit (a local map from 0 to 5) or past any skill (world maps to 150): the classic
    # numbers.
    ("cartography", "local map"): "12 100 700",
    ("cartography", "city map"): "12 250 850",
    ("cartography", "sea chart"): "12 350 950",
    ("cartography", "world map"): "12 395 995",
}

# What every recipe of a craft takes besides UOX3's list: an alchemist pours each potion into an empty bottle.
RECIPE_EXTRAS = {"alchemy": [("bottles", 1)]}

# The lists the extras name, by id: the templates each holds.
EXTRA_LISTS = {"bottles": ["0x0f0e_empty_bottle"]}

# The names of the recipes UOX3 names alike, by the item they make: the world maps of each facet.
NAMES_BY_ITEM = {
    "ilshenarmap": "World map of Ilshenar",
    "malasmap": "World map of Malas",
    "tokunomap": "World map of Tokuno",
    "termurmap": "World map of Ter Mur",
}

# The items UOX3 makes in place of the one a recipe is named for (the tinker's tools, not the 10-stone tool kit), and the
# single shaft, arrow and bolt among the stacks that share their graphic.
ITEM_FIXES = {
    "0x1eb9": "0x1ebc_tinker's_tools",
    # The plain potions, which UOX3 names by their graphic alone: the stronger ones of that graphic share it.
    "0x0f06": "0x0f06_black_potion",
    "0x0f07": "0x0f07_orange_potion",
    "0x0f08": "0x0f08_blue_potion",
    "0x0f09": "0x0f09_white_potion",
    "0x0f0a": "0x0f0a_green_potion",
    "0x0f0b": "0x0f0b_red_potion",
    "0x0f0c": "0x0f0c_yellow_potion",
    "0x0f0d": "0x0f0d_purple_potion",
    "0x1bd4": "0x1bd4_shaft",
    "0x0f3f": "0x0f3f_arrow",
    "0x1bfb": "0x1bfb_crossbow_bolt",
}

# The resources UOX3 tells apart by hue and MORE, which Moongate's templates tell apart by id, by the craft's skill, the
# recipe's name and the resource's graphic. UOX3 bakes the pizzas from the quiche and the meat pie: they take the uncooked pizzas. A raw cut
# is in UOX3's list of raw meat: each one cooks its own cut.
RESOURCE_FIXES = {
    ("cooking", "cake mix", "0x103d"): "sweet_dough",
    ("cooking", "cookie mix", "0x103d"): "sweet_dough",
    ("cooking", "muffin", "0x103d"): "sweet_dough",
    ("cooking", "cake", "0x103f"): "cake_mix",
    ("cooking", "baked quiche", "0x1042"): "unbaked_quiche",
    ("cooking", "baked meat pie", "0x1042"): "unbaked_meat_pie",
    ("cooking", "sausage pizza", "0x1042"): "uncooked_sausage_pizza",
    ("cooking", "cheese pizza", "0x1042"): "uncooked_cheese_pizza",
    ("cooking", "baked fruit pie", "0x1042"): "unbaked_fruit_pie",
    ("cooking", "baked peach cobbler", "0x1042"): "unbaked_peach_cobbler",
    ("cooking", "baked apple pie", "0x1042"): "unbaked_apple_pie",
    ("cooking", "baked pumpkin pie", "0x1042"): "unbaked_pumpkin_pie",
    ("cooking", "chicken leg", "0x1607"): "0x1607_raw_chicken_leg",
    ("cooking", "leg of lamb", "0x1609"): "0x1609_raw_leg_of_lamb",
    ("cooking", "cut of ribs", "0x09f1"): "0x09f1_cut_of_raw_ribs",
}

# The graphics a resource list counts beyond UOX3's: the closed sacks of flour, which UOX3 opens by a script first, and
# the blank map vendors sell.
LIST_EXTRAS = {"flour": [0x1039, 0x1045], "maps": [0x14EC]}

# The graphics a resource list leaves out of UOX3's: the blank scroll is what a scribe writes on, not a map.
LIST_SKIPS = {"maps": [0x0E34]}

# The groups whose names UOX3 misspells.
GROUP_FIXES = {"Miscellaneuos": "Miscellaneous"}

# UOX3's skill numbers are the client's skill ids: the names of data/skills.toml (a test checks them).
SKILL_NAMES: dict[int, str] = {
    0: "alchemy", 1: "anatomy", 2: "animal_lore", 3: "item_identification", 4: "arms_lore", 5: "parrying", 6: "begging",
    7: "blacksmithy", 8: "bowcraft_fletching", 9: "peacemaking", 10: "camping", 11: "carpentry", 12: "cartography", 13: "cooking",
    14: "detecting_hidden", 15: "discordance", 16: "evaluating_intelligence", 17: "healing", 18: "fishing", 19: "forensic_evaluation",
    20: "herding", 21: "hiding", 22: "provocation", 23: "inscription", 24: "lockpicking", 25: "magery", 26: "resisting_spells",
    27: "tactics", 28: "snooping", 29: "musicianship", 30: "poisoning", 31: "archery", 32: "spirit_speak", 33: "stealing",
    34: "tailoring", 35: "animal_taming", 36: "taste_identification", 37: "tinkering", 38: "tracking", 39: "veterinary",
    40: "swordsmanship", 41: "mace_fighting", 42: "fencing", 43: "wrestling", 44: "lumberjacking", 45: "mining", 46: "meditation",
    47: "stealth", 48: "remove_trap",
}  # fmt: skip

# UOX3 counts logs as wood; in Moongate wood is boards, and logs are sawn with an axe first.
NOT_WOOD = {0x1BDD, 0x1BE0}

RESOURCES_FILE = "resources.dfn"


class ConversionError(Exception):
    """A recipe or a list the converter cannot write."""


def run(source: Path, items: Path, destination: Path, output: TextIO, error: TextIO, spells: Path | None = None) -> int:
    """Converts the resource lists and every known craft of ``source`` (UOX3's ``dfndata/create``) into ``destination``; with
    ``spells`` (``data/spells.toml``) also the scrolls a scribe writes, as the craft of inscription."""
    source, items, destination = (Path(os.path.abspath(path)) for path in (source, items, destination))

    if not (source / RESOURCES_FILE).is_file():
        error.write(f"{RESOURCES_FILE} is missing from {source}\n")

        return 2

    try:
        templates = _template_ids(items)
        lists, list_of_graphic = _resource_lists(source / RESOURCES_FILE, templates, error)
        crafts = {
            CRAFTS[name][0]: _craft(source / f"{name}.dfn", name, lists, list_of_graphic, templates)
            for name in CRAFTS
            if (source / f"{name}.dfn").is_file()
        }

        if spells is not None:
            crafts[INSCRIPTION[0]] = _inscription(Path(os.path.abspath(spells)), lists, templates)
    except (ConversionError, OSError, tomllib.TOMLDecodeError) as exception:
        error.write(f"Crafts conversion failed: {exception}\n")

        return 2

    destination.mkdir(parents=True, exist_ok=True)
    (destination / "resources.toml").write_text(_write_lists(lists), encoding="utf-8")

    for name, craft in crafts.items():
        (destination / f"{name}.toml").write_text(_write_craft(name, craft), encoding="utf-8")
        count = sum(len(group["recipes"]) for group in craft["groups"])
        output.write(f"{name}: {count} recipe(s) in {len(craft['groups'])} group(s)\n")

    return 0


def _template_ids(items: Path) -> list[str]:
    ids: list[str] = []

    for path in sorted(items.rglob("*.toml")):
        ids.extend(item["id"] for item in tomllib.loads(path.read_text(encoding="utf-8")).get("item", []) if "id" in item)

    return ids


def _resolve(value: str, templates: list[str]) -> list[str]:
    """The templates of a UOX3 item id: the one named so, else every one whose id is the graphic followed by its name."""
    # UOX3 names the stronger potions of a graphic with a letter after a dash (0x0F0C-b): the template has an underscore.
    key = ITEM_FIXES.get(value.lower(), value.lower()).replace("-", "_")

    if key in templates:
        return [key]

    return sorted(template for template in templates if template.startswith(key + "_"))


def _resource_lists(path: Path, templates: list[str], error: TextIO) -> tuple[dict[str, list[str]], dict[int, str]]:
    """The lists by name, and the first list each graphic belongs to."""
    lists: dict[str, list[str]] = {}
    list_of_graphic: dict[int, str] = {}

    for block in dfn.parse(read_lines(path)):
        parts = block.header.split()

        if len(parts) != 2 or parts[0].upper() != "RESOURCE":
            continue

        name = parts[1].lower()
        found: list[str] = []
        ids = [trim(value) for key, _, value in (entry.partition("=") for entry in block.entries) if trim(key).upper() == "ID"]

        for value in ids + [f"0x{extra:04x}" for extra in LIST_EXTRAS.get(name, [])]:
            graphic = dfn.uox_number(value)

            if graphic is None or (name == "wood" and graphic in NOT_WOOD) or graphic in LIST_SKIPS.get(name, []):
                continue

            list_of_graphic.setdefault(graphic, name)

            found.extend(template for template in _resolve(f"0x{graphic:04x}", templates) if template not in found)

        if found:
            lists[name] = sorted(found)
        else:
            error.write(f"The resource list {name} has no template: left out\n")

    return lists, {graphic: name for graphic, name in list_of_graphic.items() if name in lists}


def _inscription(path: Path, lists: dict[str, list[str]], templates: list[str]) -> dict:
    """The craft of inscription from the spells of data/spells.toml: a scroll a spell, in the groups of its circles, taking the
    reagents of the spell and a blank scroll. Adds the list of blank scrolls (and any list of a reagent) to ``lists``."""
    craft_id, title, skill, sound = INSCRIPTION

    if not path.is_file():
        raise ConversionError(f"the spells file {path} is missing")

    spells = tomllib.loads(path.read_text(encoding="utf-8")).get("spell", [])
    lists[BLANK_SCROLLS[0]] = BLANK_SCROLLS[1]

    if any(template not in templates for template in BLANK_SCROLLS[1]):
        raise ConversionError(f"the blank scroll {BLANK_SCROLLS[1][0]} is no item template")

    groups: list[dict] = [{"name": name, "recipes": []} for name in CIRCLE_NAMES]

    for spell in sorted((spell for spell in spells if spell.get("enabled", True)), key=lambda spell: spell["id"]):
        circle = spell["circle"]

        if not 1 <= circle <= len(CIRCLE_NAMES):
            raise ConversionError(f"the spell {spell['key']} is of the circle {circle}, which is none")

        if spell["scroll"] not in templates:
            raise ConversionError(f"the scroll {spell['scroll']} of the spell {spell['key']} is no item template")

        resources = [(_reagent_list(reagent["template"], lists, templates), reagent["amount"]) for reagent in spell["reagents"]]
        resources.append((BLANK_SCROLLS[0], 1))
        low = INSCRIPTION_SKILL[circle - 1]
        groups[circle - 1]["recipes"].append(
            {
                "name": spell["name"],
                "item": spell["scroll"],
                "skills": [(skill, low, low + INSCRIPTION_WINDOW)],
                "resources": resources,
                "spell": spell["key"],
                "mana": INSCRIPTION_MANA[circle - 1],
            }
        )

    return {"name": title, "skill": skill, "sound": sound, "groups": [group for group in groups if group["recipes"]]}


def _reagent_list(template: str, lists: dict[str, list[str]], templates: list[str]) -> str:
    """The resource list a reagent counts by: the list that holds it, or the one it and its stacks of ten make; the template
    itself when it has none."""
    for name, held in lists.items():
        if template in held:
            return name

    found = _resolve(template[:6], templates)
    stacked = _one_item_in_stacks(found)

    if stacked is not None and template in found:
        lists[stacked] = sorted(found)

        return stacked

    return template


def _blocks(path: Path) -> dict[str, dfn.DfnBlock]:
    return {block.header.upper(): block for block in dfn.parse(read_lines(path))}


def _values(block: dfn.DfnBlock, tag: str) -> list[str]:
    values: list[str] = []

    for entry in block.entries:
        key, _, value = entry.partition("=")

        if trim(key).upper() == tag:
            values.append(trim(value))

    return values


def _craft(path: Path, name: str, lists: dict[str, list[str]], list_of_graphic: dict[int, str], templates: list[str]) -> dict:
    craft_id, title, skill, root = CRAFTS[name]
    blocks = _blocks(path)
    groups: list[dict] = []
    seen = {str(root)}

    # UOX3 nests its menus (Blacksmithing, Armor, Ringmail): a menu that holds recipes is a group, in the order they are
    # met; a menu already walked, such as the "Previous Menu" links, is not followed again.
    def walk(menu: str) -> None:
        for entry_number in _values(blocks[f"SUBMENU {menu}"], "MENU"):
            entry = blocks.get(f"MENUENTRY {entry_number}")

            # A link back to a menu with no entry of its own leads nowhere new.
            if entry is None:
                continue

            submenu = entry.fields["SUBMENU"]
            group_name = entry.fields["NAME"]

            if submenu in seen or group_name.lower() in SKIPPED_GROUPS:
                continue

            seen.add(submenu)
            recipes = recipes_of(submenu)

            if recipes:
                groups.append({"name": GROUP_FIXES.get(group_name, group_name), "recipes": recipes})

            walk(submenu)

    def recipes_of(menu: str) -> list[dict]:
        recipes = []

        for number in _values(blocks[f"SUBMENU {menu}"], "ITEM"):
            recipe = _recipe(blocks[f"ITEM {number}"], skill, lists, list_of_graphic, templates)

            if recipe is not None:
                recipes.append(recipe)

        return recipes

    try:
        if craft_id in ROOT_GROUPS:
            groups.append({"name": ROOT_GROUPS[craft_id], "recipes": recipes_of(str(root))})

        walk(str(root))

        for extra in EXTRA_ROOTS.get(craft_id, []):
            seen.add(extra)
            walk(extra)
    except KeyError as missing:
        raise ConversionError(f"{path.name} names {missing.args[0]}, which it does not have") from missing

    # Recipes of one name (a spoon facing either way) are told apart in the gump: the second is "Spoon 2".
    met: dict[str, int] = {}

    for group in groups:
        for recipe in group["recipes"]:
            met[recipe["name"]] = met.get(recipe["name"], 0) + 1

            if met[recipe["name"]] > 1:
                recipe["name"] = f"{recipe['name']} {met[recipe['name']]}"

    return {"name": title, "skill": skill, "sound": SOUNDS[craft_id], "groups": groups}


def _recipe(
    block: dfn.DfnBlock, craft_skill: str, lists: dict[str, list[str]], list_of_graphic: dict[int, str], templates: list[str]
) -> dict | None:
    name = block.fields.get("NAME", block.header)
    added, _, count = block.fields["ADDITEM"].partition(",")
    added = added.strip()

    # A batch (five shafts) is the recipe of one made five times: Make last repeats it.
    if added.lower() in SKIPPED_ITEMS or count.strip() not in ("", "1"):
        return None

    item = _resolve(added, templates)

    if len(item) != 1:
        raise ConversionError(f"the recipe {name} makes {added}, which is not one item template")

    skills = []
    skill_lines = _values(block, "SKILL")

    if (craft_skill, name.lower()) in SKILL_FIXES and skill_lines:
        skill_lines[0] = SKILL_FIXES[(craft_skill, name.lower())]

    for value in skill_lines:
        number, low, high = (int(part) for part in value.split()[:3])

        if number not in SKILL_NAMES:
            raise ConversionError(f"the recipe {name} names the skill {number}, which is no skill")

        skills.append((SKILL_NAMES[number], round(low / 10, 1), round(high / 10, 1)))

    if not skills or skills[0][0] != craft_skill:
        raise ConversionError(f"the first skill of the recipe {name} is not {craft_skill}")

    resources = []

    for value in _values(block, "RESOURCE"):
        what, amount = (value.split() + ["1"])[:2]
        graphic = dfn.uox_number(what) if what.lower().startswith("0x") else None

        if (craft_skill, name.lower(), what.lower()) in RESOURCE_FIXES:
            resource = RESOURCE_FIXES[(craft_skill, name.lower(), what.lower())]

            if resource not in templates:
                raise ConversionError(f"the recipe {name} is fixed to take {resource}, which is no item template")
        elif graphic is None:
            resource = what.lower()

            if resource not in lists:
                raise ConversionError(f"the recipe {name} names the resource list {what}, which has no template")
        elif graphic in NOT_WOOD:
            resource = "wood"
        elif graphic in list_of_graphic:
            # UOX3 names some ingots and cloth by their graphic: they are the list, so the player reads which one lacks.
            resource = list_of_graphic[graphic]
        else:
            found = _resolve(what, templates)
            stacked = _one_item_in_stacks(found)

            if stacked is not None:
                # The same item sold one by one and by the ten, as a reagent: both count, as a list of its own.
                if lists.get(stacked, sorted(found)) != sorted(found):
                    raise ConversionError(f"the recipe {name} makes a list {stacked}, which is another list already")

                lists[stacked] = sorted(found)
                resource = stacked
            elif len(found) != 1:
                raise ConversionError(f"the recipe {name} names the resource {what}, which is not one item template")
            else:
                resource = found[0]

        resources.append((resource, int(amount)))

    for extra, amount in RECIPE_EXTRAS.get(craft_skill, []):
        held = EXTRA_LISTS.get(extra, [extra])

        if any(template not in templates for template in held):
            raise ConversionError(f"the recipe {name} takes {extra}, which is no item template")

        if extra in EXTRA_LISTS:
            lists[extra] = held

        resources.append((extra, amount))

    shown = NAMES_BY_ITEM.get(added.lower(), NAME_FIXES.get(name, name))

    # The gump shows it as a title: UOX3 writes some in lower case.
    return {"name": shown[:1].upper() + shown[1:], "item": item[0], "skills": skills, "resources": resources}


def _one_item_in_stacks(found: list[str]) -> str | None:
    """The name of the item several templates of one graphic are, one alone and some in stacks (0x0f85_ginseng and
    0x0f85_10_ginseng); None when they are not that."""
    if len(found) < 2:
        return None

    # One template alone (0x0f85_ginseng), the others its name after a number (0x0f85_10_ginseng).
    singles = [template for template in found if re.match(r"^0x[0-9a-f]{4}_[a-z]", template)]

    if len(singles) != 1:
        return None

    name = singles[0][7:]

    if all(template == singles[0] or re.match(rf"^0x[0-9a-f]{{4}}_\d+_{re.escape(name)}$", template) for template in found):
        return name

    return None


def _write_lists(lists: dict[str, list[str]]) -> str:
    lines = [
        "# ==============================================================================",
        "# Moongate - data/crafts/resources.toml",
        "#",
        "# What it is for:",
        "#   The lists of item templates a resource of a recipe may name: \"wood\" is",
        "#   any of the plain boards. A recipe takes from the stacks of these templates",
        "#   in the backpack and its bags.",
        "#",
        "# Fields:",
        "#   [[resource]]   one list",
        "#     id           the name recipes give it (resources = [{ resource = \"wood\", ... }])",
        "#     templates    the item templates that count for it",
        "# ==============================================================================",
    ]

    for name, templates in lists.items():
        lines += ["", "[[resource]]", f"id = {tomlout.basic(name)}", f"templates = {tomlout.strings(templates)}"]

    return "\n".join(lines) + "\n"


def _write_craft(name: str, craft: dict) -> str:
    lines = [
        "# ==============================================================================",
        f"# Moongate - data/crafts/{name}.toml",
        "#",
        "# What it is for:",
        f"#   The recipes of {craft['name'].lower()}, in the groups of its crafting gump. A",
        "#   player makes one with a tool of the craft, from the resources in the",
        "#   backpack; scripts/common/crafting.lua holds the rules.",
        "#",
        "# Fields:",
        "#   id, name        the craft, as scripts open it and as its gump shows it",
        "#   skill           the main skill of every recipe",
        "#   sound           played at each stroke",
        "#   [[group]]       a group of the gump: name",
        "#   [[group.recipe]]",
        "#     name          as the gump shows it",
        "#     item          the item template made",
        "#     skill_min     the least of the main skill to try it: the chance is half; below zero, always tried",
        "#     skill_max     the skill at which it never fails",
        "#     resources     what it takes: a list of resources.toml or an item template, and the amount",
        "#     skills        other skills it asks for, with their least and most",
        "#     spell         the key of a spell (data/spells.toml) the crafter must have in a spellbook it carries; left out for none",
        "#     mana          the mana a try takes, spent on a failure too; left out for none",
        "# ==============================================================================",
        "",
        f"id = {tomlout.basic(name)}",
        f"name = {tomlout.basic(craft['name'])}",
        f"skill = {tomlout.basic(craft['skill'])}",
        f"sound = 0x{craft['sound']:04X}",
    ]

    for group in craft["groups"]:
        lines += ["", "[[group]]", f"name = {tomlout.basic(group['name'])}"]

        for recipe in group["recipes"]:
            _, low, high = recipe["skills"][0]
            resources = ", ".join(f"{{ resource = {tomlout.basic(resource)}, amount = {amount} }}" for resource, amount in recipe["resources"])
            skills = ", ".join(
                f"{{ skill = {tomlout.basic(skill)}, min = {low_:.1f}, max = {high_:.1f} }}" for skill, low_, high_ in recipe["skills"][1:]
            )
            lines += [
                "",
                "[[group.recipe]]",
                f"name = {tomlout.basic(recipe['name'])}",
                f"item = {tomlout.basic(recipe['item'])}",
                f"skill_min = {low:.1f}",
                f"skill_max = {high:.1f}",
                f"resources = [{resources}]",
                f"skills = [{skills}]",
            ]

            if "spell" in recipe:
                lines += [f"spell = {tomlout.basic(recipe['spell'])}", f"mana = {recipe['mana']}"]

    return "\n".join(lines) + "\n"
