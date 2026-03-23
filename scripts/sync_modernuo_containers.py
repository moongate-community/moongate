#!/usr/bin/env python3

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Optional

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from scripts.modernuo_item_template_tooling import (
    ROOT_ITEMS_DIRECTORY,
    load_json_array,
    normalize_template_description,
    write_json_array,
)

CLASS_PATTERN = re.compile(
    r"(?P<prefix>(?:\[[^\]]+\]\s*)*)public\s+(?:abstract\s+)?partial\s+class\s+(?P<name>\w+)\s*:\s*(?P<base>\w+)",
    re.MULTILINE,
)
CONSTRUCTOR_PATTERN = r"public\s+{class_name}\s*\([^)]*\)\s*:\s*base\(\s*(?P<item_id>0x[0-9A-Fa-f]+|\d+)"
WEIGHT_PATTERN = re.compile(r"public\s+override\s+double\s+DefaultWeight\s*=>\s*(?P<value>-?\d+(?:\.\d+)?)\s*;")
FLIPPABLE_PATTERN = re.compile(r"\[Flippable\((?P<args>[^\)]*)\)\]")
NUMERIC_LITERAL_PATTERN = re.compile(r"0x[0-9A-Fa-f]+|\d+")

DEFAULT_WEIGHT_MAX = 40000
DEFAULT_MAX_ITEMS = 125

EXCLUDED_CLASS_NAMES = frozenset(
    {
        "Pouch",
        "DecorativeBox",
        "StrongBox",
        "MarkContainer",
        "ParagonChest",
        "TreasureMapChest",
        "WoodenTreasureChest",
        "MetalGoldenTreasureChest",
        "MetalTreasureChest",
        "SalvageBag",
    }
)

EXCLUDED_BASE_NAMES = frozenset({"TrappableContainer", "LockableContainer", "FillableContainer", "BaseTreasureChest"})


@dataclass(frozen=True)
class ClassDefinition:
    name: str
    base_name: str
    body: str


def to_snake_case(value: str) -> str:
    return re.sub(r"[\s-]+", "_", re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)).lower()


def to_display_name(value: str) -> str:
    snake = to_snake_case(value)
    return " ".join(part.capitalize() for part in snake.split("_"))


def normalize_item_id(raw_value: str) -> str:
    value = int(raw_value, 0)
    return f"0x{value:04X}"


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


def extract_flippable_item_ids(class_body: str) -> list[str]:
    match = FLIPPABLE_PATTERN.search(class_body)
    if match is None:
        return []

    return [normalize_item_id(value) for value in NUMERIC_LITERAL_PATTERN.findall(match.group("args"))]


def load_container_gump_ids(path: Path) -> tuple[Optional[str], Dict[int, str]]:
    default_gump_id: Optional[str] = None
    mapping: Dict[int, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue

        parts = [segment.strip() for segment in line.split("\t")]
        if len(parts) < 4:
            if len(parts) >= 1 and default_gump_id is None:
                default_gump_id = normalize_item_id(parts[0])
            continue

        gump_id = normalize_item_id(parts[0])
        for item_literal in parts[3].split(","):
            item_literal = item_literal.strip()
            if not item_literal:
                continue

            mapping[int(item_literal, 0)] = gump_id

    return default_gump_id, mapping


def load_container_layout_ids(path: Path) -> Dict[str, str]:
    definitions = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(definitions, list):
        raise ValueError(f"{path} must contain a JSON array")

    mapping: Dict[str, str] = {}
    for definition in definitions:
        if not isinstance(definition, dict):
            continue

        layout_id = definition.get("Id")
        if not isinstance(layout_id, str) or not layout_id:
            continue

        mapping[layout_id] = layout_id

    return mapping


def is_normal_container(definitions: Dict[str, ClassDefinition], class_name: str) -> bool:
    current_name = class_name
    visited: set[str] = set()
    while current_name not in visited:
        visited.add(current_name)
        if current_name in EXCLUDED_CLASS_NAMES or current_name in EXCLUDED_BASE_NAMES:
            return False

        if current_name == "BaseContainer":
            return True

        definition = definitions.get(current_name)
        if definition is None:
            return False

        current_name = definition.base_name

    return False


def scan_container_metadata(
    source_root: Path,
    gump_definitions_path: Path,
    layout_definitions_path: Path,
) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(source_root)
    default_gump_id, gump_ids = load_container_gump_ids(gump_definitions_path)
    layout_ids = load_container_layout_ids(layout_definitions_path)

    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        if not is_normal_container(definitions, definition.name):
            continue

        item_id = extract_constructible_item_id(definition.name, definition.body)
        if item_id is None:
            continue

        numeric_item_id = int(item_id, 0)
        template_id = to_snake_case(definition.name)
        metadata[template_id] = {
            "name": to_display_name(definition.name),
            "description": "",
            "itemId": item_id,
            "weight": extract_weight(definition.body),
            "gumpId": gump_ids.get(numeric_item_id) or (default_gump_id if template_id in layout_ids else None),
            "containerLayoutId": layout_ids.get(template_id),
            "weightMax": DEFAULT_WEIGHT_MAX,
            "maxItems": DEFAULT_MAX_ITEMS,
            "flippableItemIds": extract_flippable_item_ids(definition.body),
        }

    return metadata


def build_tags(existing_tags: object, flippable_item_ids: list[str]) -> list[str]:
    tags: list[str] = ["modernuo", "containers"]
    if isinstance(existing_tags, list):
        for tag in existing_tags:
            if not isinstance(tag, str):
                continue

            normalized = tag.strip()
            if not normalized or normalized in tags or normalized == "flippable":
                continue

            tags.append(normalized)

    if flippable_item_ids:
        tags.append("flippable")

    return tags


def sync_template_file(template_file: Path, metadata_by_id: Dict[str, Dict[str, object]]) -> None:
    existing_templates = load_json_array(template_file) if template_file.exists() else []
    existing_by_id = {str(template.get("id", "")): template for template in existing_templates}

    synced: list[dict[str, object]] = []
    for template_id in sorted(metadata_by_id):
        metadata = metadata_by_id[template_id]
        template = dict(existing_by_id.get(template_id, {}))

        template["type"] = "item"
        template["id"] = template_id
        template["name"] = metadata["name"]
        template["category"] = "Containers"
        template["description"] = normalize_template_description(template.get("description"))
        template["itemId"] = metadata["itemId"]
        template["hue"] = template.get("hue", "0")
        template["goldValue"] = template.get("goldValue", "0")
        template["weight"] = metadata["weight"]
        template["scriptId"] = "none"
        template["isMovable"] = template.get("isMovable", True)
        template["weightMax"] = metadata["weightMax"]
        template["maxItems"] = metadata["maxItems"]
        template["tags"] = build_tags(template.get("tags", []), metadata["flippableItemIds"])

        gump_id = metadata.get("gumpId")
        if isinstance(gump_id, str) and gump_id:
            template["gumpId"] = gump_id
        else:
            template.pop("gumpId", None)

        container_layout_id = metadata.get("containerLayoutId")
        if isinstance(container_layout_id, str) and container_layout_id:
            template["containerLayoutId"] = container_layout_id
        else:
            template.pop("containerLayoutId", None)

        flippable_item_ids = metadata["flippableItemIds"]
        if flippable_item_ids:
            template["flippableItemIds"] = flippable_item_ids
        else:
            template.pop("flippableItemIds", None)

        synced.append(template)

    write_json_array(template_file, synced)


def sync_container_metadata(
    source_root: Path,
    template_file: Path,
    gump_definitions_path: Path,
    layout_definitions_path: Path,
) -> None:
    metadata = scan_container_metadata(source_root, gump_definitions_path, layout_definitions_path)
    sync_template_file(template_file, metadata)


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
        if (candidate / "Projects" / "UOContent" / "Items" / "Containers").exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --source-root.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Sync normal ModernUO containers into Moongate root item templates.")
    parser.add_argument(
        "source_root",
        nargs="?",
        help="Path to ModernUO Projects/UOContent/Items/Containers",
    )
    parser.add_argument(
        "template_file",
        nargs="?",
        default=str(ROOT_ITEMS_DIRECTORY / "containers.json"),
        help="Path to Moongate containers.json template file",
    )
    parser.add_argument(
        "--gump-definitions",
        default="moongate_data/data/containers/containers.cfg",
        help="Path to Moongate containers.cfg file.",
    )
    parser.add_argument(
        "--layout-definitions",
        default="moongate_data/data/containers/default_containers.json",
        help="Path to Moongate default_containers.json file.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    if args.source_root:
        source_root = Path(args.source_root).expanduser().resolve()
    else:
        modernuo_root = discover_modernuo_root(repo_root)
        source_root = modernuo_root / "Projects" / "UOContent" / "Items" / "Containers"

    template_file = Path(args.template_file).expanduser().resolve()
    gump_definitions_path = Path(args.gump_definitions).expanduser().resolve()
    layout_definitions_path = Path(args.layout_definitions).expanduser().resolve()
    sync_container_metadata(source_root, template_file, gump_definitions_path, layout_definitions_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
