"""UOX3's create menus (``dfndata/create``) into the crafts of Moongate (``data/crafts``): one file a craft, with its groups and
recipes, and ``resources.toml`` with the lists of templates a recipe's resource may name."""

from __future__ import annotations

import os
import tomllib
from pathlib import Path
from typing import TextIO

from . import dfn, tomlout
from .textutil import read_lines, trim

# The crafts this converter knows: the UOX3 file name -> (the name shown, the skill, the root submenu of its menus).
CRAFTS: dict[str, tuple[str, str, int]] = {"carpentry": ("Carpentry", "carpentry", 19)}

# The sound UOX3 plays for every recipe of a craft.
SOUNDS: dict[str, int] = {"carpentry": 0x023D}

# The groups that make deeds, which mean nothing until houses exist.
SKIPPED_GROUPS = {"house additions", "blacksmith add-ons", "tailor add-ons", "cooking add-ons"}

# What an axe already does: logs sawn into boards.
SKIPPED_ITEMS = {"0x1bd7"}

NAME_FIXES = {"Magincian Throne": "Magician Throne"}

# UOX3's skill numbers are the client's skill ids: the names of data/skills.toml.
SKILL_NAMES: dict[int, str] = {
    0: "alchemy", 1: "anatomy", 2: "animal_lore", 3: "item_identification", 4: "arms_lore", 5: "parrying", 6: "begging",
    7: "blacksmithing", 8: "bowcraft_fletching", 9: "peacemaking", 10: "camping", 11: "carpentry", 12: "cartography", 13: "cooking",
    14: "detecting_hidden", 15: "discordance", 16: "evaluating_intelligence", 17: "healing", 18: "fishing", 19: "forensic_evaluation",
    20: "herding", 21: "hiding", 22: "provocation", 23: "inscription", 24: "lockpicking", 25: "magery", 26: "magic_resistance",
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


def run(source: Path, items: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    """Converts the resource lists and every known craft of ``source`` (UOX3's ``dfndata/create``) into ``destination``."""
    source, items, destination = (Path(os.path.abspath(path)) for path in (source, items, destination))

    if not (source / RESOURCES_FILE).is_file():
        error.write(f"{RESOURCES_FILE} is missing from {source}\n")

        return 2

    try:
        templates = _template_ids(items)
        lists = _resource_lists(source / RESOURCES_FILE, templates, error)
        crafts = {name: _craft(source / f"{name}.dfn", name, lists, templates) for name in CRAFTS if (source / f"{name}.dfn").is_file()}
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
    key = value.lower()

    if key in templates:
        return [key]

    return sorted(template for template in templates if template.startswith(key + "_"))


def _resource_lists(path: Path, templates: list[str], error: TextIO) -> dict[str, list[str]]:
    lists: dict[str, list[str]] = {}

    for block in dfn.parse(read_lines(path)):
        parts = block.header.split()

        if len(parts) != 2 or parts[0].upper() != "RESOURCE":
            continue

        name = parts[1].lower()
        found: list[str] = []

        for entry in block.entries:
            key, _, value = entry.partition("=")
            graphic = dfn.uox_number(trim(value)) if trim(key).upper() == "ID" else None

            if graphic is None or (name == "wood" and graphic in NOT_WOOD):
                continue

            found.extend(template for template in _resolve(f"0x{graphic:04x}", templates) if template not in found)

        if found:
            lists[name] = sorted(found)
        else:
            error.write(f"The resource list {name} has no template: left out\n")

    return lists


def _blocks(path: Path) -> dict[str, dfn.DfnBlock]:
    return {block.header.upper(): block for block in dfn.parse(read_lines(path))}


def _values(block: dfn.DfnBlock, tag: str) -> list[str]:
    values: list[str] = []

    for entry in block.entries:
        key, _, value = entry.partition("=")

        if trim(key).upper() == tag:
            values.append(trim(value))

    return values


def _craft(path: Path, name: str, lists: dict[str, list[str]], templates: list[str]) -> dict:
    title, skill, root = CRAFTS[name]
    blocks = _blocks(path)
    groups: list[dict] = []

    for menu in _values(blocks[f"SUBMENU {root}"], "MENU"):
        entry = blocks[f"MENUENTRY {menu}"]
        group_name = entry.fields["NAME"]

        if group_name.lower() in SKIPPED_GROUPS:
            continue

        recipes = []

        for number in _values(blocks[f"SUBMENU {entry.fields['SUBMENU']}"], "ITEM"):
            recipe = _recipe(blocks[f"ITEM {number}"], skill, lists, templates)

            if recipe is not None:
                recipes.append(recipe)

        groups.append({"name": group_name, "recipes": recipes})

    return {"name": title, "skill": skill, "sound": SOUNDS[name], "groups": groups}


def _recipe(block: dfn.DfnBlock, craft_skill: str, lists: dict[str, list[str]], templates: list[str]) -> dict | None:
    name = block.fields.get("NAME", block.header)
    added = block.fields["ADDITEM"].split(",")[0].strip()

    if added.lower() in SKIPPED_ITEMS:
        return None

    item = _resolve(added, templates)

    if len(item) != 1:
        raise ConversionError(f"the recipe {name} makes {added}, which is not one item template")

    skills = []

    for value in _values(block, "SKILL"):
        number, low, high = (int(part) for part in value.split()[:3])

        if number not in SKILL_NAMES:
            raise ConversionError(f"the recipe {name} names the skill {number}, which is no skill")

        skills.append((SKILL_NAMES[number], round(low / 10, 1), round(high / 10, 1)))

    if not skills or skills[0][0] != craft_skill:
        raise ConversionError(f"the first skill of the recipe {name} is not {craft_skill}")

    resources = []

    for value in _values(block, "RESOURCE"):
        what, amount = value.split()[:2]
        graphic = dfn.uox_number(what) if what.lower().startswith("0x") else None

        if graphic is None:
            resource = what.lower()

            if resource not in lists:
                raise ConversionError(f"the recipe {name} names the resource list {what}, which has no template")
        elif graphic in NOT_WOOD:
            resource = "wood"
        else:
            found = _resolve(what, templates)

            if len(found) != 1:
                raise ConversionError(f"the recipe {name} names the resource {what}, which is not one item template")

            resource = found[0]

        resources.append((resource, int(amount)))

    return {"name": NAME_FIXES.get(name, name), "item": item[0], "skills": skills, "resources": resources}


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
        "#     skill_min     the least of the main skill to try it: the chance is half",
        "#     skill_max     the skill at which it never fails",
        "#     resources     what it takes: a list of resources.toml or an item template, and the amount",
        "#     skills        other skills it asks for, with their least and most",
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

    return "\n".join(lines) + "\n"
