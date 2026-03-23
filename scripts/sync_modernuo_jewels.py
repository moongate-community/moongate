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

from scripts.modernuo_item_template_tooling import (
    ROOT_ITEMS_DIRECTORY,
    load_json_array,
    normalize_script_id,
    normalize_template_description,
    write_json_array,
)

CLASS_PATTERN = re.compile(
    r"public\s+(?:abstract\s+)?partial\s+class\s+(?P<name>\w+)\s*:\s*(?P<base>\w+)",
    re.MULTILINE,
)
CONSTRUCTOR_PATTERN = r"public\s+{class_name}\s*\([^)]*\)\s*:\s*base\(\s*(?P<item_id>0x[0-9A-Fa-f]+|\d+)"
LAYER_FROM_BASE_PATTERN = re.compile(r":\s*base\([^)]*Layer\.(?P<layer>\w+)", re.MULTILINE)
HIT_POINTS_PATTERN = re.compile(r"public\s+override\s+int\s+InitMaxHits\s*=>\s*(?P<value>-?\d+)\s*;")


@dataclass(frozen=True)
class ClassDefinition:
    name: str
    base_name: str
    body: str


def to_snake_case(value: str) -> str:
    return re.sub(r"[\s-]+", "_", re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)).lower()


def normalize_item_id(raw_value: str) -> str:
    value = int(raw_value, 0)
    return f"0x{value:04X}"


def parse_class_definitions(root: Path) -> Dict[str, ClassDefinition]:
    definitions: Dict[str, ClassDefinition] = {}
    for path in sorted(root.rglob("*.cs")):
        if "Artifacts" in path.parts:
            continue

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


def extract_hit_points(class_body: str) -> int:
    match = HIT_POINTS_PATTERN.search(class_body)
    if match is None:
        return 0

    return int(match.group("value"))


def resolve_jewelry_layer(definitions: Dict[str, ClassDefinition], class_name: str) -> Optional[str]:
    current_name = class_name
    visited: set[str] = set()
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        base_match = LAYER_FROM_BASE_PATTERN.search(definition.body)
        if base_match is not None:
            return base_match.group("layer")

        current_name = definition.base_name

    return None


def scan_jewelry_metadata(root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(root)
    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        item_id = extract_constructible_item_id(definition.name, definition.body)
        if item_id is None:
            continue

        layer = resolve_jewelry_layer(definitions, definition.name)
        if layer is None:
            continue

        metadata[to_snake_case(definition.name)] = {
            "itemId": item_id,
            "layer": layer,
            "hitPoints": extract_hit_points(definition.body),
        }

    return metadata


def sync_template_file(template_file: Path, metadata_by_id: Dict[str, Dict[str, object]]) -> None:
    templates = load_json_array(template_file)
    for template in templates:
        template_id = str(template.get("id", ""))
        metadata = metadata_by_id.get(template_id)
        if metadata is None:
            continue

        template["description"] = normalize_template_description(template.get("description"))
        template["itemId"] = metadata["itemId"]
        template["layer"] = metadata["layer"]
        template["hitPoints"] = metadata["hitPoints"]
        template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))

    write_json_array(template_file, templates)


def sync_jewelry_metadata(source_root: Path, template_file: Path) -> None:
    sync_template_file(template_file, scan_jewelry_metadata(source_root))


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
        if (candidate / "Projects" / "UOContent" / "Items" / "Jewels").exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --source-root.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Sync ModernUO jewelry metadata into Moongate root item templates.")
    parser.add_argument(
        "source_root",
        nargs="?",
        help="Path to ModernUO Projects/UOContent/Items/Jewels",
    )
    parser.add_argument(
        "template_file",
        nargs="?",
        default=str(ROOT_ITEMS_DIRECTORY / "jewels.json"),
        help="Path to Moongate jewels.json template file",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    if args.source_root:
        source_root = Path(args.source_root).expanduser().resolve()
    else:
        modernuo_root = discover_modernuo_root(repo_root)
        source_root = modernuo_root / "Projects" / "UOContent" / "Items" / "Jewels"

    template_file = Path(args.template_file).expanduser().resolve()
    sync_jewelry_metadata(source_root, template_file)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
