#!/usr/bin/env python3

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Sequence

ROOT_ITEMS_DIRECTORY = Path("moongate_data/templates/items")
ROOT_LOOT_DIRECTORY = Path("moongate_data/templates/loot")

TYPE_TO_TAG = {
    "Amber": "gems",
    "ArchCureScroll": "skill items",
    "BaseArmor": "armor",
    "BaseHat": "clothing",
    "BaseJewel": "jewels",
    "BaseRanged": "weapons",
    "BaseShield": "shields",
    "BaseWeapon": "weapons",
    "ClumsyScroll": "skill items",
    "SummonAirElementalScroll": "skill items",
}

TYPE_TO_ITEM_ID = {
    "Gold": "gold",
    "HorseShoes": "horse_shoes",
    "Lockpick": "lockpick",
    "Lockpicks": "lockpick",
    "MortarPestle": "mortar_pestle",
    "NightSightPotion": "night_sight_potion",
    "RawLambLeg": "raw_lamb_leg",
    "SheafOfHay": "sheaf_of_hay",
    "SledgeHammer": "sledge_hammer",
    "SmithHammer": "smith_hammer",
    "TinkerTools": "tinker_tools",
}


@dataclass(frozen=True)
class DiceRange:
    minimum: int
    maximum: int


def load_json_array(path: Path) -> List[Dict[str, object]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{path} must contain a JSON array")

    return data


def write_json_array(path: Path, items: List[Dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f"{json.dumps(items, indent=2)}\n", encoding="utf-8")


def normalize_identifier(name: str) -> str:
    cleaned = re.sub(r"[^0-9A-Za-z]+", "_", name).strip("_")
    with_boundaries = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", cleaned)
    with_boundaries = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", with_boundaries)
    return with_boundaries.lower()


def build_output_paths(repo_root: Path) -> Dict[str, Path]:
    loot_root = repo_root / ROOT_LOOT_DIRECTORY
    return {
        "creatures": loot_root / "creatures.json",
        "treasure_chests": loot_root / "treasure_chests.json",
        "fillable_containers": loot_root / "fillable_containers.json",
    }


def build_loot_id(name: str, prefix: str) -> str:
    return f"{prefix}.{normalize_identifier(name)}"


def load_item_template_index(repo_root: Path) -> tuple[Dict[str, Dict[str, object]], Dict[str, List[str]]]:
    item_index: Dict[str, Dict[str, object]] = {}
    tag_index: Dict[str, List[str]] = {}

    for path in sorted((repo_root / ROOT_ITEMS_DIRECTORY).rglob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(data, list):
            continue

        for item in data:
            if not isinstance(item, dict):
                continue

            item_id = item.get("id")
            if not isinstance(item_id, str) or not item_id:
                continue

            normalized_id = normalize_identifier(item_id)
            item_index[normalized_id] = item

            for tag in item.get("tags", []) or []:
                if not isinstance(tag, str) or not tag:
                    continue

                tag_index.setdefault(tag, []).append(normalized_id)

    return item_index, tag_index


def resolve_item_reference(
    type_name: str, item_index: Dict[str, Dict[str, object]], tag_index: Dict[str, List[str]]
) -> Dict[str, object] | None:
    special_item_id = TYPE_TO_ITEM_ID.get(type_name)
    if special_item_id is not None and special_item_id in item_index:
        return {"itemTemplateId": special_item_id}

    normalized_id = normalize_identifier(type_name)
    if normalized_id in item_index:
        return {"itemTemplateId": normalized_id}

    mapped_tag = TYPE_TO_TAG.get(type_name)
    if mapped_tag is not None and tag_index.get(mapped_tag):
        return {"itemTag": mapped_tag}

    return None


def parse_dice_range(value: str | int) -> DiceRange:
    if isinstance(value, int):
        return DiceRange(value, value)

    stripped = value.strip().strip('"')
    if stripped.isdigit():
        integer = int(stripped)
        return DiceRange(integer, integer)

    match = re.fullmatch(r"(\d+)d(\d+)([+-]\d+)?", stripped)
    if match is None:
        raise ValueError(f"Unsupported dice expression: {value}")

    count = int(match.group(1))
    sides = int(match.group(2))
    bonus = int(match.group(3) or "0")
    return DiceRange(count + bonus, count * sides + bonus)


def split_top_level_arguments(arguments: str) -> List[str]:
    parts: List[str] = []
    current: List[str] = []
    paren_depth = 0
    bracket_depth = 0
    brace_depth = 0
    in_string = False
    string_delimiter = ""

    for char in arguments:
        if in_string:
            current.append(char)
            if char == string_delimiter:
                in_string = False
            continue

        if char in {"'", '"'}:
            in_string = True
            string_delimiter = char
            current.append(char)
            continue

        if char == "(":
            paren_depth += 1
        elif char == ")":
            paren_depth -= 1
        elif char == "[":
            bracket_depth += 1
        elif char == "]":
            bracket_depth -= 1
        elif char == "{":
            brace_depth += 1
        elif char == "}":
            brace_depth -= 1

        if char == "," and paren_depth == 0 and bracket_depth == 0 and brace_depth == 0:
            part = "".join(current).strip()
            if part:
                parts.append(part)
            current = []
            continue

        current.append(char)

    tail = "".join(current).strip()
    if tail:
        parts.append(tail)

    return parts


def extract_balanced_block(text: str, start_index: int, open_char: str, close_char: str) -> tuple[str, int]:
    if text[start_index] != open_char:
        raise ValueError(f"Expected '{open_char}' at index {start_index}")

    depth = 0
    in_string = False
    delimiter = ""

    for index in range(start_index, len(text)):
        char = text[index]

        if in_string:
            if char == delimiter:
                in_string = False
            continue

        if char in {"'", '"'}:
            in_string = True
            delimiter = char
            continue

        if char == open_char:
            depth += 1
        elif char == close_char:
            depth -= 1
            if depth == 0:
                return text[start_index + 1 : index], index + 1

    raise ValueError(f"Unbalanced block starting at index {start_index}")
