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
LIT_ITEM_PATTERN = re.compile(r"public\s+override\s+int\s+LitItemID\s*=>\s*(?P<expr>[^;]+);")
UNLIT_ITEM_PATTERN = re.compile(r"public\s+override\s+int\s+UnlitItemID\s*=>\s*(?P<expr>[^;]+);")
LIT_SOUND_PATTERN = re.compile(r"public\s+override\s+int\s+LitSound\s*=>\s*(?P<value>0x[0-9A-Fa-f]+|\d+)\s*;")
UNLIT_SOUND_PATTERN = re.compile(r"public\s+override\s+int\s+UnlitSound\s*=>\s*(?P<value>0x[0-9A-Fa-f]+|\d+)\s*;")
LAYER_TWO_HANDED_PATTERN = re.compile(r"Layer\s*=\s*Layer\.TwoHanded")

DEFAULT_LIT_SOUND = "0x0047"
DEFAULT_UNLIT_SOUND = "0x03BE"
LIGHT_SCRIPT_ID = "items.light_source"
LIGHT_PARAM_KEYS = (
    "light_lit_item_id",
    "light_unlit_item_id",
    "light_burning",
    "light_toggle_sound_on",
    "light_toggle_sound_off",
)


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


def resolve_override_expression(
    definitions: Dict[str, ClassDefinition],
    class_name: str,
    pattern: re.Pattern[str],
) -> Optional[str]:
    current_name = class_name
    visited: set[str] = set()
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        match = pattern.search(definition.body)
        if match is not None:
            return match.group("expr") if "expr" in match.groupdict() else match.group("value")

        current_name = definition.base_name

    return None


def resolve_two_handed_layer(definitions: Dict[str, ClassDefinition], class_name: str) -> Optional[str]:
    current_name = class_name
    visited: set[str] = set()
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        if current_name == "BaseEquipableLight" or definition.base_name == "BaseEquipableLight" or LAYER_TWO_HANDED_PATTERN.search(definition.body):
            return "TwoHanded"

        current_name = definition.base_name

    return None


def transform_condition(condition: str) -> str:
    stripped = " ".join(condition.strip().split())
    is_match = re.fullmatch(r"ItemID\s+is\s+(?P<values>.+)", stripped)
    if is_match is not None:
        values = [token.strip() for token in is_match.group("values").split("or")]
        return f"ItemID in ({', '.join(values)})"

    return stripped


def transform_value(value: str) -> str:
    stripped = value.strip()
    if stripped == "ItemID":
        return "ItemID"

    return stripped


def evaluate_item_expression(expression: Optional[str], current_item_id: int) -> Optional[str]:
    if expression is None:
        return None

    stripped = " ".join(expression.strip().split())
    ternary_match = re.fullmatch(r"(?P<condition>.+?)\s*\?\s*(?P<when_true>.+?)\s*:\s*(?P<when_false>.+)", stripped)
    if ternary_match is not None:
        condition = transform_condition(ternary_match.group("condition"))
        when_true = transform_value(ternary_match.group("when_true"))
        when_false = transform_value(ternary_match.group("when_false"))
        python_expression = f"({when_true} if {condition} else {when_false})"
    else:
        python_expression = transform_value(stripped)

    value = eval(python_expression, {"__builtins__": {}}, {"ItemID": current_item_id})
    return normalize_item_id(str(int(value)))


def resolve_sound_value(
    definitions: Dict[str, ClassDefinition],
    class_name: str,
    pattern: re.Pattern[str],
    default_value: str,
) -> str:
    expression = resolve_override_expression(definitions, class_name, pattern)
    if expression is None:
        return default_value

    return normalize_item_id(expression)


def scan_light_metadata(root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(root)
    metadata: Dict[str, Dict[str, object]] = {}

    for definition in definitions.values():
        item_id = extract_constructible_item_id(definition.name, definition.body)
        if item_id is None:
            continue

        current_item_id = int(item_id, 0)
        lit_item_id = evaluate_item_expression(
            resolve_override_expression(definitions, definition.name, LIT_ITEM_PATTERN),
            current_item_id,
        )
        unlit_item_id = evaluate_item_expression(
            resolve_override_expression(definitions, definition.name, UNLIT_ITEM_PATTERN),
            current_item_id,
        )
        toggleable = lit_item_id is not None and unlit_item_id is not None and lit_item_id != unlit_item_id

        metadata[to_snake_case(definition.name)] = {
            "toggleable": toggleable,
            "litItemId": lit_item_id,
            "unlitItemId": unlit_item_id,
            "burning": "true" if lit_item_id == item_id else "false",
            "toggleSoundOn": resolve_sound_value(definitions, definition.name, LIT_SOUND_PATTERN, DEFAULT_LIT_SOUND),
            "toggleSoundOff": resolve_sound_value(
                definitions,
                definition.name,
                UNLIT_SOUND_PATTERN,
                DEFAULT_UNLIT_SOUND,
            ),
            "layer": resolve_two_handed_layer(definitions, definition.name),
        }

    return metadata


def build_light_params(metadata: Dict[str, object]) -> Dict[str, Dict[str, str]]:
    return {
        "light_lit_item_id": {"type": "String", "value": str(metadata["litItemId"])},
        "light_unlit_item_id": {"type": "String", "value": str(metadata["unlitItemId"])},
        "light_burning": {"type": "String", "value": str(metadata["burning"])},
        "light_toggle_sound_on": {"type": "String", "value": str(metadata["toggleSoundOn"])},
        "light_toggle_sound_off": {"type": "String", "value": str(metadata["toggleSoundOff"])},
    }


def sync_template_file(template_file: Path, metadata_by_id: Dict[str, Dict[str, object]]) -> None:
    templates = load_json_array(template_file)
    for template in templates:
        template_id = str(template.get("id", ""))
        metadata = metadata_by_id.get(template_id)
        if metadata is None:
            continue

        template["description"] = normalize_template_description(template.get("description"))
        params = {
            key: value
            for key, value in dict(template.get("params", {})).items()
            if key not in LIGHT_PARAM_KEYS
        }

        if metadata["toggleable"]:
            template["scriptId"] = LIGHT_SCRIPT_ID
            params.update(build_light_params(metadata))
            template["params"] = params
        else:
            template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))
            if params:
                template["params"] = params
            else:
                template.pop("params", None)

        layer = metadata.get("layer")
        if layer:
            template["layer"] = layer

    write_json_array(template_file, templates)


def sync_light_metadata(source_root: Path, template_file: Path) -> None:
    sync_template_file(template_file, scan_light_metadata(source_root))


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
        if (candidate / "Projects" / "UOContent" / "Items" / "Lights").exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --source-root.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Sync ModernUO light metadata into Moongate root item templates.")
    parser.add_argument(
        "source_root",
        nargs="?",
        help="Path to ModernUO Projects/UOContent/Items/Lights",
    )
    parser.add_argument(
        "template_file",
        nargs="?",
        default=str(ROOT_ITEMS_DIRECTORY / "lights.json"),
        help="Path to Moongate lights.json template file",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    if args.source_root:
        source_root = Path(args.source_root).expanduser().resolve()
    else:
        modernuo_root = discover_modernuo_root(repo_root)
        source_root = modernuo_root / "Projects" / "UOContent" / "Items" / "Lights"

    template_file = Path(args.template_file).expanduser().resolve()
    sync_light_metadata(source_root, template_file)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
