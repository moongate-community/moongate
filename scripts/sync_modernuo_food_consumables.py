#!/usr/bin/env python3

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Optional

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from scripts.modernuo_item_template_tooling import ROOT_ITEMS_DIRECTORY, load_json_array, normalize_template_description, write_json_array

CLASS_PATTERN = re.compile(
    r"public\s+(?:abstract\s+)?partial\s+class\s+(?P<name>\w+)\s*:\s*(?P<base>\w+)",
    re.MULTILINE,
)
CONSTRUCTOR_PATTERN = r"public\s+{class_name}\s*\([^)]*\)\s*:\s*base\(\s*(?P<item_id>0x[0-9A-Fa-f]+|\d+)"
WEIGHT_PATTERN = re.compile(r"public\s+override\s+double\s+DefaultWeight\s*=>\s*(?P<value>-?\d+(?:\.\d+)?)\s*;")

FOOD_SCRIPT_ID = "items.food"
BEVERAGE_SCRIPT_ID = "items.beverage"

BEVERAGE_VARIANTS = (
    ("bottle_ale", "Bottle Of Ale", "BottleAle", "0x099F", 1),
    ("bottle_liquor", "Bottle Of Liquor", "BottleLiquor", "0x099B", 1),
    ("bottle_wine", "Bottle Of Wine", "BottleWine", "0x09C7", 1),
    ("pitcher_ale", "Pitcher Of Ale", "PitcherAle", "0x1F95", 2),
    ("pitcher_cider", "Pitcher Of Cider", "PitcherCider", "0x1F97", 2),
    ("pitcher_liquor", "Pitcher Of Liquor", "PitcherLiquor", "0x1F99", 2),
    ("pitcher_milk", "Pitcher Of Milk", "PitcherMilk", "0x09F0", 2),
    ("pitcher_wine", "Pitcher Of Wine", "PitcherWine", "0x1F9B", 2),
    ("pitcher_water", "Pitcher Of Water", "PitcherWater", "0x1F9D", 2),
    ("mug_ale", "Mug Of Ale", "MugAle", "0x09EE", 1),
    ("glass_cider", "Glass Of Cider", "GlassCider", "0x1F7D", 1),
    ("glass_liquor", "Glass Of Liquor", "GlassLiquor", "0x1F85", 1),
    ("glass_milk", "Glass Of Milk", "GlassMilk", "0x1F89", 1),
    ("glass_wine", "Glass Of Wine", "GlassWine", "0x1F8D", 1),
    ("glass_water", "Glass Of Water", "GlassWater", "0x1F91", 1),
)


@dataclass(frozen=True)
class ClassDefinition:
    name: str
    base_name: str
    body: str


def to_snake_case(value: str) -> str:
    return re.sub(r"[\s-]+", "_", re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)).lower()


def parse_class_definitions(root: Path) -> Dict[str, ClassDefinition]:
    definitions: Dict[str, ClassDefinition] = {}
    for path in sorted(root.rglob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        matches = list(CLASS_PATTERN.finditer(text))
        for index, match in enumerate(matches):
            start = match.start()
            end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
            definition = ClassDefinition(
                name=match.group("name"),
                base_name=match.group("base"),
                body=text[start:end],
            )
            definitions[definition.name] = definition

    return definitions


def normalize_item_id(raw_value: str) -> str:
    value = int(raw_value, 0)
    return f"0x{value:04X}"


def extract_constructible_item_id(class_name: str, class_body: str) -> Optional[str]:
    pattern = re.compile(CONSTRUCTOR_PATTERN.format(class_name=re.escape(class_name)))
    match = pattern.search(class_body)
    if match is None:
        return None

    return normalize_item_id(match.group("item_id"))


def extract_weight(class_body: str) -> float:
    match = WEIGHT_PATTERN.search(class_body)
    if match is None:
        return 1.0

    return float(match.group("value"))


def is_edible_food(definitions: Dict[str, ClassDefinition], class_name: str) -> bool:
    current_name = class_name
    visited: set[str] = set()
    while current_name not in visited:
        visited.add(current_name)
        if current_name in {"CookableFood", "BaseBeverage"}:
            return False

        if current_name == "Food":
            return True

        definition = definitions.get(current_name)
        if definition is None:
            return False

        current_name = definition.base_name

    return False


def scan_food_metadata(source_root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(source_root)
    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        if not is_edible_food(definitions, definition.name):
            continue

        item_id = extract_constructible_item_id(definition.name, definition.body)
        if item_id is None:
            continue

        metadata[to_snake_case(definition.name)] = {
            "itemId": item_id,
            "weight": extract_weight(definition.body),
        }

    return metadata


def build_beverage_templates() -> Dict[str, Dict[str, object]]:
    templates: Dict[str, Dict[str, object]] = {}
    for template_id, name, alias_name, item_id, weight in BEVERAGE_VARIANTS:
        templates[template_id] = {
            "type": "item",
            "id": template_id,
            "name": name,
            "category": "Food",
            "description": "",
            "itemId": item_id,
            "hue": "0",
            "goldValue": "0",
            "weight": weight,
            "scriptId": BEVERAGE_SCRIPT_ID,
            "isMovable": True,
            "tags": ["modernuo", "food", "beverage"],
        }

    return templates


def build_food_tags(existing_tags: object) -> list[str]:
    tags = ["modernuo", "food"]
    if isinstance(existing_tags, list):
        for tag in existing_tags:
            if not isinstance(tag, str):
                continue

            normalized = tag.strip()
            if not normalized or normalized in tags:
                continue

            tags.append(normalized)

    return tags


def sync_template_file(
    template_file: Path,
    food_metadata_by_id: Dict[str, Dict[str, object]],
    beverage_templates_by_id: Dict[str, Dict[str, object]],
) -> None:
    templates = load_json_array(template_file) if template_file.exists() else []
    existing_ids = {str(template.get("id", "")) for template in templates}

    for template in templates:
        template_id = str(template.get("id", ""))
        template["description"] = normalize_template_description(template.get("description"))
        if template_id in food_metadata_by_id:
            metadata = food_metadata_by_id[template_id]
            template["itemId"] = metadata["itemId"]
            template["weight"] = metadata["weight"]
            template["scriptId"] = FOOD_SCRIPT_ID
            template["tags"] = build_food_tags(template.get("tags", []))
            continue

        if template_id in beverage_templates_by_id:
            template.update(beverage_templates_by_id[template_id])
            continue

        template["scriptId"] = "none"

    for template_id in sorted(beverage_templates_by_id):
        if template_id in existing_ids:
            continue

        templates.append(dict(beverage_templates_by_id[template_id]))

    templates.sort(key=lambda item: str(item.get("id", "")))
    write_json_array(template_file, templates)


def sync_food_consumables(source_root: Path, template_file: Path) -> None:
    food_metadata = scan_food_metadata(source_root)
    beverage_templates = build_beverage_templates()
    sync_template_file(template_file, food_metadata, beverage_templates)


def discover_modernuo_root(repo_root: Path) -> Path:
    env_root = os.environ.get("MODERNUO_ROOT")
    candidates = []
    if env_root:
        candidates.append(Path(env_root).expanduser())

    candidates.extend(
        [
            repo_root.parent / "others" / "ModernUO",
            Path("/Users/squid/projects/others/ModernUO"),
        ]
    )

    for candidate in candidates:
        if (candidate / "Projects" / "UOContent" / "Items" / "Food").exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --source-root.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Sync simple ModernUO food and beverage consumables into root templates.")
    parser.add_argument(
        "source_root",
        nargs="?",
        help="Path to ModernUO Projects/UOContent/Items/Food",
    )
    parser.add_argument(
        "template_file",
        nargs="?",
        default=str(ROOT_ITEMS_DIRECTORY / "food.json"),
        help="Path to Moongate food.json template file",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    if args.source_root:
        source_root = Path(args.source_root).expanduser().resolve()
    else:
        modernuo_root = discover_modernuo_root(repo_root)
        source_root = modernuo_root / "Projects" / "UOContent" / "Items" / "Food"

    template_file = Path(args.template_file).expanduser().resolve()
    sync_food_consumables(source_root, template_file)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
