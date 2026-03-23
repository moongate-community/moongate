#!/usr/bin/env python3

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, Optional

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from scripts.modernuo_item_template_tooling import ROOT_ITEMS_DIRECTORY, normalize_script_id, normalize_template_description

CLASS_PATTERN = re.compile(
    r"(?P<prefix>(?:\[[^\]]+\]\s*)*)public\s+(?:abstract\s+)?partial\s+class\s+(?P<name>\w+)\s*:\s*(?P<base>\w+)",
    re.MULTILINE,
)
CONSTRUCTOR_PATTERN = r"public\s+{class_name}\s*\([^)]*\)\s*:\s*base\(\s*(?P<item_id>0x[0-9A-Fa-f]+|\d+)"
LAYER_FROM_BASE_PATTERN = re.compile(r":\s*base\([^)]*Layer\.(?P<layer>\w+)", re.MULTILINE)
LAYER_OVERRIDE_PATTERN = re.compile(r"Layer\s*=\s*Layer\.(?P<layer>\w+)")
INT_OVERRIDE_PATTERNS = {
    "strength": re.compile(r"public\s+override\s+int\s+AosStrReq\s*=>\s*(?P<value>-?\d+)\s*;"),
    "dexterity": re.compile(r"public\s+override\s+int\s+AosDexReq\s*=>\s*(?P<value>-?\d+)\s*;"),
    "intelligence": re.compile(r"public\s+override\s+int\s+AosIntReq\s*=>\s*(?P<value>-?\d+)\s*;"),
    "hitPoints": re.compile(r"public\s+override\s+int\s+InitMaxHits\s*=>\s*(?P<value>-?\d+)\s*;"),
}

ARMOR_LAYER_SUFFIXES = (
    ("_arms_type1", "Arms"),
    ("_arms_type2", "Arms"),
    ("_arms", "Arms"),
    ("_pauldrons", "Arms"),
    ("_bracers", "Arms"),
    ("_hiro_sode", "Arms"),
    ("_gloves", "Gloves"),
    ("_mitts", "Gloves"),
    ("_gauntlets", "Gloves"),
    ("_gorget", "Neck"),
    ("_collar", "Neck"),
    ("_helm", "Helm"),
    ("_coif", "Helm"),
    ("_hatsuburi", "Helm"),
    ("_jingasa", "Helm"),
    ("_kabuto", "Helm"),
    ("_mempo", "Helm"),
    ("_cap", "Helm"),
    ("_circlet", "Helm"),
    ("_helmet", "Helm"),
    ("_crown", "Helm"),
    ("_glasses", "Helm"),
    ("_hood", "Helm"),
    ("_do", "InnerTorso"),
    ("_chest_type1", "InnerTorso"),
    ("_chest_type2", "InnerTorso"),
    ("_chest", "InnerTorso"),
    ("_breastplate", "InnerTorso"),
    ("_jacket", "InnerTorso"),
    ("_wing_armor", "InnerTorso"),
    ("_resolution", "InnerTorso"),
    ("_legs_type1", "InnerLegs"),
    ("_legs_type2", "InnerLegs"),
    ("_legs", "InnerLegs"),
    ("_leggings", "InnerLegs"),
    ("_haidate", "InnerLegs"),
    ("_pants", "Pants"),
    ("_shorts", "Pants"),
    ("_skirt", "OuterLegs"),
    ("_kilt_type1", "OuterLegs"),
    ("_kilt_type2", "OuterLegs"),
    ("_tonlet", "OuterLegs"),
    ("_suneate", "Shoes"),
)

ARMOR_LAYER_SPECIAL_CASES = {
    "bascinet": "Helm",
    "circlet": "Helm",
    "helmet": "Helm",
}


@dataclass(frozen=True)
class ClassDefinition:
    name: str
    base_name: str
    body: str


def to_snake_case(value: str) -> str:
    return re.sub(r"[\s-]+", "_", re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)).lower()


def load_json_array(path: Path) -> list[dict[str, object]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{path} must contain a JSON array")

    return data


def write_json_array(path: Path, data: list[dict[str, object]]) -> None:
    path.write_text(f"{json.dumps(data, indent=2)}\n", encoding="utf-8")


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


def extract_numeric_override(class_body: str, key: str) -> int:
    match = INT_OVERRIDE_PATTERNS[key].search(class_body)
    if match is None:
        return 0

    return int(match.group("value"))


def extract_constructible_item_id(class_name: str, class_body: str) -> Optional[str]:
    pattern = re.compile(CONSTRUCTOR_PATTERN.format(class_name=re.escape(class_name)))
    match = pattern.search(class_body)
    if match is None:
        return None

    return normalize_item_id(match.group("item_id"))


def normalize_item_id(raw_value: str) -> str:
    value = int(raw_value, 0)
    return f"0x{value:04X}"


def resolve_clothing_layer(definitions: Dict[str, ClassDefinition], class_name: str) -> Optional[str]:
    current_name = class_name
    visited: set[str] = set()
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        override_match = LAYER_OVERRIDE_PATTERN.search(definition.body)
        if override_match is not None:
            return override_match.group("layer")

        base_match = LAYER_FROM_BASE_PATTERN.search(definition.body)
        if base_match is not None:
            return base_match.group("layer")

        current_name = definition.base_name

    return None


def resolve_armor_layer(template_id: str) -> Optional[str]:
    if template_id in ARMOR_LAYER_SPECIAL_CASES:
        return ARMOR_LAYER_SPECIAL_CASES[template_id]

    for suffix, layer in ARMOR_LAYER_SUFFIXES:
        if template_id.endswith(suffix):
            return layer

    return None


def build_metadata_record(item_id: str, layer: str, class_body: str) -> Dict[str, object]:
    return {
        "itemId": item_id,
        "layer": layer,
        "strength": extract_numeric_override(class_body, "strength"),
        "dexterity": extract_numeric_override(class_body, "dexterity"),
        "intelligence": extract_numeric_override(class_body, "intelligence"),
        "hitPoints": extract_numeric_override(class_body, "hitPoints"),
    }


def scan_clothing_metadata(root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(root)
    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        item_id = extract_constructible_item_id(definition.name, definition.body)
        if item_id is None:
            continue

        layer = resolve_clothing_layer(definitions, definition.name)
        if layer is None:
            continue

        metadata[to_snake_case(definition.name)] = build_metadata_record(item_id, layer, definition.body)

    return metadata


def scan_armor_metadata(root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(root)
    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        item_id = extract_constructible_item_id(definition.name, definition.body)
        if item_id is None:
            continue

        template_id = to_snake_case(definition.name)
        layer = resolve_armor_layer(template_id)
        if layer is None:
            continue

        metadata[template_id] = build_metadata_record(item_id, layer, definition.body)

    return metadata


def sync_template_file(template_file: Path, metadata_by_id: Dict[str, Dict[str, object]]) -> None:
    templates = load_json_array(template_file)
    for template in templates:
        template_id = str(template.get("id", ""))
        if template_id not in metadata_by_id:
            continue

        metadata = metadata_by_id[template_id]
        template["description"] = normalize_template_description(template.get("description"))
        template["itemId"] = metadata["itemId"]
        template["layer"] = metadata["layer"]
        template["strength"] = metadata["strength"]
        template["dexterity"] = metadata["dexterity"]
        template["intelligence"] = metadata["intelligence"]
        template["hitPoints"] = metadata["hitPoints"]
        template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))

    write_json_array(template_file, templates)


def sync_wearable_metadata(
    clothing_root: Path,
    armor_root: Path,
    clothing_template_file: Path,
    armor_template_file: Path,
) -> None:
    sync_template_file(clothing_template_file, scan_clothing_metadata(clothing_root))
    sync_template_file(armor_template_file, scan_armor_metadata(armor_root))


def discover_modernuo_root(repo_root: Path) -> Path:
    env_root = os.environ.get("MODERNUO_ROOT")
    candidates = []
    if env_root:
        candidates.append(Path(env_root).expanduser())

    candidates.extend(
        [
            repo_root.parent.parent / "others" / "ModernUO",
            repo_root.parent / "ModernUO",
            Path.home() / "projects" / "others" / "ModernUO",
        ]
    )

    for candidate in candidates:
        clothing_root = candidate / "Projects" / "UOContent" / "Items" / "Clothing"
        armor_root = candidate / "Projects" / "UOContent" / "Items" / "Armor"
        if clothing_root.exists() and armor_root.exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --modernuo-root.")


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Sync ModernUO clothing and armor equip metadata into Moongate root item templates.")
    parser.add_argument("--modernuo-root")
    parser.add_argument("--items-root", default=str(ROOT_ITEMS_DIRECTORY))
    return parser


def main(argv: Optional[Iterable[str]] = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(list(argv) if argv is not None else None)
    repo_root = Path(__file__).resolve().parent.parent
    modernuo_root = Path(args.modernuo_root).expanduser() if args.modernuo_root else discover_modernuo_root(repo_root)
    items_root = Path(args.items_root)

    sync_wearable_metadata(
        modernuo_root / "Projects" / "UOContent" / "Items" / "Clothing",
        modernuo_root / "Projects" / "UOContent" / "Items" / "Armor",
        items_root / "clothing.json",
        items_root / "armor.json",
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
