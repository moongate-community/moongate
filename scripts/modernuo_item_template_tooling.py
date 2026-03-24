#!/usr/bin/env python3

import json
import re
from pathlib import Path
from typing import Dict, Iterable, List

ROOT_ITEMS_DIRECTORY = Path("moongate_data/templates/items")
MODERNUO_ITEMS_ROOT = ROOT_ITEMS_DIRECTORY / "modernuo"
IMPORTED_DESCRIPTION_PATTERN = re.compile(r"^Imported from ModernUO(?: alias)? \(.+\)\.$")

PRESERVED_SCRIPT_IDS = frozenset(
    {
        "items.apple",
        "items.bb",
        "items.brick",
        "items.bulletin_board",
        "items.door",
        "items.dye_box",
        "items.dye_tub",
        "items.ethereal_horse",
        "items.food",
        "items.beverage",
        "items.light_source",
        "items.spawn",
        "items.teleport",
    }
)


def load_json_array(path: Path) -> List[Dict[str, object]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{path} must contain a JSON array")

    return data


def write_json_array(path: Path, items: List[Dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f"{json.dumps(items, indent=2)}\n", encoding="utf-8")


def normalize_script_id(script_id: str) -> str:
    if script_id in PRESERVED_SCRIPT_IDS:
        return script_id

    return "none"


def normalize_template_description(description: object) -> str:
    if not isinstance(description, str):
        return ""

    normalized = description.strip()
    if not normalized:
        return ""

    if IMPORTED_DESCRIPTION_PATTERN.fullmatch(normalized) is not None:
        return ""

    return normalized


def normalize_template_script_ids(items: Iterable[Dict[str, object]]) -> List[Dict[str, object]]:
    normalized: List[Dict[str, object]] = []
    for item in items:
        normalized_item = dict(item)
        normalized_item["scriptId"] = normalize_script_id(str(item.get("scriptId", "")))
        normalized.append(normalized_item)

    return normalized


def normalize_template_descriptions(items: Iterable[Dict[str, object]]) -> List[Dict[str, object]]:
    normalized: List[Dict[str, object]] = []
    for item in items:
        normalized_item = dict(item)
        normalized_item["description"] = normalize_template_description(item.get("description"))
        normalized.append(normalized_item)

    return normalized


def migrate_modernuo_item_templates(source_root: Path, target_root: Path) -> List[Path]:
    if not source_root.exists():
        return []

    target_root.mkdir(parents=True, exist_ok=True)
    migrated_files: List[Path] = []
    source_files = sorted(source_root.glob("*.json"))

    for source_file in source_files:
        target_file = target_root / source_file.name
        if target_file.exists():
            raise FileExistsError(f"Target file already exists: {target_file.name}")

        normalized_items = normalize_template_descriptions(normalize_template_script_ids(load_json_array(source_file)))
        write_json_array(target_file, normalized_items)
        source_file.unlink()
        migrated_files.append(target_file)

    if source_root.exists():
        source_root.rmdir()

    return migrated_files
