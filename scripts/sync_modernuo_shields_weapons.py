#!/usr/bin/env python3

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional

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
    r"(?P<prefix>(?:\[[^\]]+\]\s*)*)public\s+(?:abstract\s+)?partial\s+class\s+(?P<name>\w+)\s*:\s*(?P<base>\w+)",
    re.MULTILINE,
)
CONSTRUCTIBLE_CTOR_PATTERN = r"\[Constructible\]\s*public\s+{class_name}\s*\([^)]*\)\s*:\s*base\((?P<args>[^)]*)\)"
CONSTRUCTIBLE_CTOR_EXISTS_PATTERN = r"\[Constructible\]\s*public\s+{class_name}\s*\("
NUMERIC_OVERRIDE_PATTERNS = {
    "weight": re.compile(r"public\s+override\s+double\s+DefaultWeight\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "strength": re.compile(r"public\s+override\s+int\s+Aos(?:Strength|Str)Req\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "dexterity": re.compile(r"public\s+override\s+int\s+Aos(?:Dexterity|Dex)Req\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "intelligence": re.compile(r"public\s+override\s+int\s+Aos(?:Intelligence|Int)Req\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "hitPoints": re.compile(r"public\s+override\s+int\s+InitMaxHits\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "aosMinDamage": re.compile(r"public\s+override\s+int\s+AosMinDamage\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "aosMaxDamage": re.compile(r"public\s+override\s+int\s+AosMaxDamage\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "oldMinDamage": re.compile(r"public\s+override\s+int\s+OldMinDamage\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "oldMaxDamage": re.compile(r"public\s+override\s+int\s+OldMaxDamage\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "speed": re.compile(r"public\s+override\s+int\s+AosSpeed\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "defMaxRange": re.compile(r"public\s+override\s+int\s+DefMaxRange\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "aosMaxRange": re.compile(r"public\s+override\s+int\s+AosMaxRange\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "effectId": re.compile(r"public\s+override\s+int\s+EffectID\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "hitSound": re.compile(r"public\s+(?:override|virtual)\s+int\s+DefHitSound\s*=>\s*(?P<expr>[^;]+);", re.MULTILINE),
    "missSound": re.compile(
        r"public\s+(?:override|virtual)\s+int\s+DefMissSound\s*=>\s*(?P<expr>[^;]+);",
        re.MULTILINE,
    ),
}
WEAPON_SKILL_PATTERN = re.compile(r"public\s+override\s+SkillName\s+DefSkill\s*=>\s*SkillName\.(?P<value>\w+)\s*;", re.MULTILINE)
ANIMATION_PATTERN = re.compile(r"public\s+override\s+WeaponAnimation\s+DefAnimation\s*=>\s*WeaponAnimation\.(?P<value>\w+)\s*;", re.MULTILINE)
LAYER_ASSIGN_PATTERN = re.compile(r"Layer\s*=\s*Layer\.(?P<value>\w+)")
AMMO_TYPE_PATTERN = re.compile(r"public\s+override\s+Type\s+AmmoType\s*=>\s*(?P<expr>null|typeof\((?P<type>\w+)\))\s*;", re.MULTILINE)
FLIPPABLE_PATTERN = re.compile(r"\[Flippable\((?P<args>[^)]*)\)\]", re.MULTILINE)
NUMERIC_LITERAL_PATTERN = re.compile(r"0x[0-9A-Fa-f]+|\d+(?:\.\d+)?")

PRESERVED_BOOK_TEMPLATE_IDS = frozenset({"welcome_player"})
AMMO_TYPE_TO_IDS = {
    "Arrow": ("0x0F3F", "0x1BFE"),
    "Bolt": ("0x1BFB", "0x1BFB"),
}


@dataclass(frozen=True)
class ClassDefinition:
    name: str
    base_name: str
    body: str
    prefix: str
    relative_path: str


def to_snake_case(value: str) -> str:
    first_pass = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", value)
    second_pass = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", first_pass)
    return re.sub(r"[\s-]+", "_", second_pass).lower()


def to_display_name(class_name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", " ", class_name).strip()


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
                prefix=match.group("prefix"),
                relative_path=path.relative_to(root).as_posix(),
            )
            definitions[definition.name] = definition

    return definitions


def extract_constructible_args(definition: ClassDefinition) -> Optional[str]:
    pattern = re.compile(CONSTRUCTIBLE_CTOR_PATTERN.format(class_name=re.escape(definition.name)), re.MULTILINE | re.DOTALL)
    match = pattern.search(definition.body)
    if match is None:
        return None

    return match.group("args")


def has_constructible_constructor(definition: ClassDefinition) -> bool:
    pattern = re.compile(CONSTRUCTIBLE_CTOR_EXISTS_PATTERN.format(class_name=re.escape(definition.name)), re.MULTILINE | re.DOTALL)
    return pattern.search(definition.body) is not None


def extract_numeric_literal(expression: str, use_last: bool = False) -> Optional[str]:
    matches = NUMERIC_LITERAL_PATTERN.findall(expression)
    if not matches:
        return None

    return matches[-1] if use_last else matches[0]


def normalize_item_id(raw_value: str) -> str:
    value = int(raw_value, 0)
    return f"0x{value:04X}"


def extract_constructible_item_id(definition: ClassDefinition) -> Optional[str]:
    args = extract_constructible_args(definition)
    if args is None:
        return None

    literal = extract_numeric_literal(args)
    if literal is None:
        return None

    return normalize_item_id(literal)


def resolve_constructible_item_id(definitions: Dict[str, ClassDefinition], class_name: str) -> Optional[str]:
    visited: set[str] = set()
    current_name = class_name
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        item_id = extract_constructible_item_id(definition)
        if item_id is not None:
            return item_id

        current_name = definition.base_name

    return None


def extract_numeric_override(definition: ClassDefinition, key: str, *, use_last: bool = False) -> Optional[int]:
    match = NUMERIC_OVERRIDE_PATTERNS[key].search(definition.body)
    if match is None:
        return None

    literal = extract_numeric_literal(match.group("expr"), use_last=use_last)
    if literal is None:
        return None

    return int(float(literal)) if "." in literal else int(literal, 0)


def resolve_recursive_string(definitions: Dict[str, ClassDefinition], class_name: str, pattern: re.Pattern[str], group: str) -> Optional[str]:
    visited: set[str] = set()
    current_name = class_name
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        match = pattern.search(definition.body)
        if match is not None:
            return match.group(group)

        current_name = definition.base_name

    return None


def resolve_recursive_numeric(
    definitions: Dict[str, ClassDefinition],
    class_name: str,
    key: str,
    *,
    use_last: bool = False,
) -> Optional[int]:
    visited: set[str] = set()
    current_name = class_name
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            return None

        value = extract_numeric_override(definition, key, use_last=use_last)
        if value is not None:
            return value

        current_name = definition.base_name

    return None


def resolve_weapon_skill(definitions: Dict[str, ClassDefinition], class_name: str) -> Optional[str]:
    return resolve_recursive_string(definitions, class_name, WEAPON_SKILL_PATTERN, "value")


def resolve_layer(definitions: Dict[str, ClassDefinition], class_name: str) -> str:
    explicit = resolve_recursive_string(definitions, class_name, LAYER_ASSIGN_PATTERN, "value")
    if explicit is not None:
        return explicit

    animation = resolve_recursive_string(definitions, class_name, ANIMATION_PATTERN, "value") or ""
    if animation.startswith("Shoot") or animation.endswith("2H"):
        return "TwoHanded"

    return "OneHanded"


def resolve_flippable(definition: ClassDefinition) -> bool:
    return FLIPPABLE_PATTERN.search(definition.prefix) is not None


def resolve_ammo(definitions: Dict[str, ClassDefinition], class_name: str) -> tuple[Optional[str], Optional[str]]:
    visited: set[str] = set()
    current_name = class_name
    while current_name not in visited:
        visited.add(current_name)
        definition = definitions.get(current_name)
        if definition is None:
            break

        match = AMMO_TYPE_PATTERN.search(definition.body)
        if match is not None:
            ammo_type = match.group("type")
            if ammo_type is None:
                return (None, None)

            return AMMO_TYPE_TO_IDS.get(ammo_type, (None, None))

        current_name = definition.base_name

    return (None, None)


def merge_tags(existing: object, additions: List[str]) -> List[str]:
    merged: List[str] = []
    for tag in list(existing) if isinstance(existing, list) else []:
        value = str(tag)
        if value not in merged:
            merged.append(value)

    for tag in additions:
        if tag not in merged:
            merged.append(tag)

    return merged


def build_shield_metadata(definitions: Dict[str, ClassDefinition], definition: ClassDefinition) -> Dict[str, object]:
    item_id = resolve_constructible_item_id(definitions, definition.name)
    if item_id is None:
        raise ValueError(f"Shield {definition.name} does not define a constructible item id.")

    weight = extract_numeric_override(definition, "weight") or 0
    strength = extract_numeric_override(definition, "strength") or 0
    hit_points = extract_numeric_override(definition, "hitPoints") or 0
    tags = ["modernuo", "shields"]
    if resolve_flippable(definition):
        tags.append("flippable")

    return {
        "name": to_display_name(definition.name),
        "description": "",
        "itemId": item_id,
        "weight": weight,
        "layer": "TwoHanded",
        "strength": strength,
        "hitPoints": hit_points,
        "tags": tags,
    }


def choose_weapon_damage(metadata: Dict[str, object], *, use_old_damage: bool) -> tuple[int, int]:
    if use_old_damage and metadata.get("oldLowDamage") is not None and metadata.get("oldHighDamage") is not None:
        return int(metadata["oldLowDamage"]), int(metadata["oldHighDamage"])

    return int(metadata.get("aosLowDamage") or 0), int(metadata.get("aosHighDamage") or 0)


def build_weapon_metadata(definitions: Dict[str, ClassDefinition], definition: ClassDefinition) -> Dict[str, object]:
    item_id = resolve_constructible_item_id(definitions, definition.name)
    if item_id is None:
        raise ValueError(f"Weapon {definition.name} does not define a constructible item id.")

    weapon_skill = resolve_weapon_skill(definitions, definition.name)
    weight = resolve_recursive_numeric(definitions, definition.name, "weight") or 0
    strength = resolve_recursive_numeric(definitions, definition.name, "strength") or 0
    dexterity = resolve_recursive_numeric(definitions, definition.name, "dexterity") or 0
    intelligence = resolve_recursive_numeric(definitions, definition.name, "intelligence") or 0
    hit_points = resolve_recursive_numeric(definitions, definition.name, "hitPoints") or 0
    speed = resolve_recursive_numeric(definitions, definition.name, "speed") or 0
    aos_low = resolve_recursive_numeric(definitions, definition.name, "aosMinDamage") or 0
    aos_high = resolve_recursive_numeric(definitions, definition.name, "aosMaxDamage") or 0
    old_low = resolve_recursive_numeric(definitions, definition.name, "oldMinDamage") or 0
    old_high = resolve_recursive_numeric(definitions, definition.name, "oldMaxDamage") or 0
    max_range = resolve_recursive_numeric(definitions, definition.name, "aosMaxRange")
    if max_range is None:
        max_range = resolve_recursive_numeric(definitions, definition.name, "defMaxRange")
    max_range = max_range or 1
    hit_sound = resolve_recursive_numeric(definitions, definition.name, "hitSound")
    miss_sound = resolve_recursive_numeric(definitions, definition.name, "missSound")
    ammo, ammo_fx = resolve_ammo(definitions, definition.name)
    layer = resolve_layer(definitions, definition.name)
    tags = ["modernuo", "weapons"]
    if resolve_flippable(definition):
        tags.append("flippable")

    use_old_damage = weapon_skill == "Archery"
    low_damage, high_damage = choose_weapon_damage(
        {
            "aosLowDamage": aos_low,
            "aosHighDamage": aos_high,
            "oldLowDamage": old_low,
            "oldHighDamage": old_high,
        },
        use_old_damage=use_old_damage,
    )

    return {
        "name": to_display_name(definition.name),
        "description": "",
        "itemId": item_id,
        "weight": weight,
        "layer": layer,
        "strength": strength,
        "dexterity": dexterity,
        "intelligence": intelligence,
        "hitPoints": hit_points,
        "weaponSkill": weapon_skill,
        "ammo": ammo,
        "ammoFx": ammo_fx,
        "baseRange": 1,
        "maxRange": max_range,
        "lowDamage": low_damage,
        "highDamage": high_damage,
        "speed": speed,
        "hitSound": hit_sound,
        "missSound": miss_sound,
        "tags": tags,
    }


def scan_shield_metadata(root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(root)
    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        if "Artifacts/" in definition.relative_path:
            continue

        if not has_constructible_constructor(definition):
            continue

        metadata[to_snake_case(definition.name)] = build_shield_metadata(definitions, definition)

    return metadata


def scan_weapon_metadata(root: Path) -> Dict[str, Dict[str, object]]:
    definitions = parse_class_definitions(root)
    metadata: Dict[str, Dict[str, object]] = {}
    for definition in definitions.values():
        if "Artifacts/" in definition.relative_path or "Abilities/" in definition.relative_path:
            continue

        if definition.name == "Fists":
            continue

        if not has_constructible_constructor(definition):
            continue

        metadata[to_snake_case(definition.name)] = build_weapon_metadata(definitions, definition)

    return metadata


def sync_shield_template_file(template_file: Path, metadata_by_id: Dict[str, Dict[str, object]]) -> None:
    templates = load_json_array(template_file)
    templates_by_id = {str(template.get("id", "")): template for template in templates}

    for template in templates:
        template_id = str(template.get("id", ""))
        metadata = metadata_by_id.get(template_id)
        if metadata is None:
            continue

        template["description"] = normalize_template_description(template.get("description"))
        template["itemId"] = metadata["itemId"]
        template["layer"] = metadata["layer"]
        template["strength"] = metadata["strength"]
        template["hitPoints"] = metadata["hitPoints"]
        if metadata.get("weight"):
            template["weight"] = metadata["weight"]
        template["tags"] = merge_tags(template.get("tags"), list(metadata.get("tags", [])))
        template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))

    for template_id, metadata in sorted(metadata_by_id.items()):
        if template_id in templates_by_id:
            continue

        templates.append(
            {
                "type": "item",
                "id": template_id,
                "name": metadata["name"],
                "category": "Shields",
                "description": normalize_template_description(metadata.get("description")),
                "itemId": metadata["itemId"],
                "hue": "0",
                "goldValue": "0",
                "weight": metadata["weight"],
                "scriptId": "none",
                "isMovable": True,
                "tags": metadata["tags"],
                "layer": metadata["layer"],
                "strength": metadata["strength"],
                "hitPoints": metadata["hitPoints"],
            }
        )

    templates.sort(key=lambda item: str(item.get("id", "")))
    write_json_array(template_file, templates)


def sync_weapon_template_file(template_file: Path, metadata_by_id: Dict[str, Dict[str, object]]) -> None:
    templates = load_json_array(template_file)
    templates_by_id = {str(template.get("id", "")): template for template in templates}

    for template in templates:
        template_id = str(template.get("id", ""))
        metadata = metadata_by_id.get(template_id)
        if metadata is None:
            continue

        template["description"] = normalize_template_description(template.get("description"))
        template["itemId"] = metadata["itemId"]
        template["layer"] = metadata["layer"]
        template["strength"] = metadata["strength"]
        template["dexterity"] = metadata["dexterity"]
        template["intelligence"] = metadata["intelligence"]
        template["hitPoints"] = metadata["hitPoints"]
        template["weaponSkill"] = metadata["weaponSkill"]
        template["baseRange"] = metadata["baseRange"]
        template["maxRange"] = metadata["maxRange"]
        template["lowDamage"] = metadata["lowDamage"]
        template["highDamage"] = metadata["highDamage"]
        template["speed"] = metadata["speed"]
        template["hitSound"] = metadata["hitSound"]
        template["missSound"] = metadata["missSound"]
        template["tags"] = merge_tags(template.get("tags"), list(metadata["tags"]))

        if metadata["weight"]:
            template["weight"] = metadata["weight"]

        if metadata["ammo"] is not None:
            template["ammo"] = metadata["ammo"]
        else:
            template.pop("ammo", None)

        if metadata["ammoFx"] is not None:
            template["ammoFx"] = metadata["ammoFx"]
        else:
            template.pop("ammoFx", None)

        template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))

    for template_id, metadata in sorted(metadata_by_id.items()):
        if template_id in templates_by_id:
            continue

        template = {
            "type": "item",
            "id": template_id,
            "name": metadata["name"],
            "category": "Weapons",
            "description": normalize_template_description(metadata.get("description")),
            "itemId": metadata["itemId"],
            "hue": "0",
            "goldValue": "0",
            "weight": metadata["weight"],
            "scriptId": "none",
            "isMovable": True,
            "tags": metadata["tags"],
            "layer": metadata["layer"],
            "lowDamage": metadata["lowDamage"],
            "highDamage": metadata["highDamage"],
            "speed": metadata["speed"],
            "hitSound": metadata["hitSound"],
            "missSound": metadata["missSound"],
            "strength": metadata["strength"],
            "dexterity": metadata["dexterity"],
            "intelligence": metadata["intelligence"],
            "hitPoints": metadata["hitPoints"],
            "weaponSkill": metadata["weaponSkill"],
            "baseRange": metadata["baseRange"],
            "maxRange": metadata["maxRange"],
        }

        if metadata["ammo"] is not None:
            template["ammo"] = metadata["ammo"]

        if metadata["ammoFx"] is not None:
            template["ammoFx"] = metadata["ammoFx"]

        templates.append(template)

    templates.sort(key=lambda item: str(item.get("id", "")))
    write_json_array(template_file, templates)


def sync_shields_and_weapons(shields_root: Path, weapons_root: Path, items_root: Path) -> None:
    sync_shield_template_file(items_root / "shields.json", scan_shield_metadata(shields_root))
    sync_weapon_template_file(items_root / "weapons.json", scan_weapon_metadata(weapons_root))


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
        shields_root = candidate / "Projects" / "UOContent" / "Items" / "Shields"
        weapons_root = candidate / "Projects" / "UOContent" / "Items" / "Weapons"
        if shields_root.exists() and weapons_root.exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --modernuo-root.")


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Sync ModernUO shields and weapons into Moongate item templates.")
    parser.add_argument("--modernuo-root")
    parser.add_argument("--items-root", default=str(ROOT_ITEMS_DIRECTORY))
    return parser


def main(argv: Optional[Iterable[str]] = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(list(argv) if argv is not None else None)
    repo_root = Path(__file__).resolve().parent.parent
    modernuo_root = Path(args.modernuo_root).expanduser() if args.modernuo_root else discover_modernuo_root(repo_root)
    items_root = Path(args.items_root)

    sync_shields_and_weapons(
        modernuo_root / "Projects" / "UOContent" / "Items" / "Shields",
        modernuo_root / "Projects" / "UOContent" / "Items" / "Weapons",
        items_root,
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
