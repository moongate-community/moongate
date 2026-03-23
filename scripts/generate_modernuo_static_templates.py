#!/usr/bin/env python3

import argparse
import json
import re
from pathlib import Path
from typing import Dict, List, Optional, Tuple

DENYLIST_CATEGORIES = {
    "addons",
    "armor",
    "books",
    "clothing",
    "jewels",
    "lights",
    "shields",
    "talismans",
    "wands",
    "weapons",
}


def to_snake_case(value: str) -> str:
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value).replace("-", "_").lower()


def should_include_item(category: str, item: Dict[str, object]) -> bool:
    if category in DENYLIST_CATEGORIES:
        return False

    if item.get("staticDecorativeCandidate") is not True:
        return False

    item_ids = item.get("itemIds")
    return isinstance(item_ids, list) and len(item_ids) > 0


def map_audit_item_to_template(source_path: str, category_id: str, item: Dict[str, object]) -> Dict[str, object]:
    template_id = to_snake_case(str(item["className"]))
    tags = ["modernuo", category_id]
    if item.get("flipIds"):
        tags.append("flippable")

    return {
        "type": "item",
        "id": template_id,
        "name": item["defaultName"],
        "category": source_path,
        "description": f"Imported from ModernUO ({item['className']}).",
        "itemId": item["itemIds"][0],
        "hue": "0",
        "goldValue": "0",
        "weight": item.get("weight") if item.get("weight") is not None else 0,
        "scriptId": f"items.{template_id}",
        "isMovable": bool(item.get("movable", True)),
        "tags": tags,
    }


def load_existing_templates(category_file: Path) -> List[Dict[str, object]]:
    if not category_file.exists():
        return []

    data = json.loads(category_file.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{category_file} must contain a JSON array")

    return data


def merge_templates_into_category_file(category_file: Path, generated_templates: List[Dict[str, object]]) -> None:
    existing_templates = load_existing_templates(category_file)
    existing_by_id = {str(template["id"]): template for template in existing_templates}

    new_templates: List[Dict[str, object]] = []
    for template in generated_templates:
        template_id = str(template["id"])
        if template_id not in existing_by_id:
            existing_by_id[template_id] = template
            new_templates.append(template)

    new_templates.sort(key=lambda template: str(template["id"]))
    final_templates = existing_templates + new_templates
    category_file.parent.mkdir(parents=True, exist_ok=True)
    category_file.write_text(f"{json.dumps(final_templates, indent=2)}\n", encoding="utf-8")


def group_templates_by_category(category_templates: List[Tuple[str, Dict[str, object]]]) -> Dict[str, List[Dict[str, object]]]:
    grouped: Dict[str, List[Dict[str, object]]] = {}
    for category_id, template in category_templates:
        grouped.setdefault(category_id, []).append(template)

    for templates in grouped.values():
        templates.sort(key=lambda template: str(template["id"]))

    return grouped


def load_audit_json(audit_file: Path) -> Dict[str, object]:
    data = json.loads(audit_file.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"{audit_file} must contain a JSON object")

    return data


def generate_from_audit_file(audit_file: Path, output_root: Path, category_filter: Optional[str] = None) -> Dict[str, int]:
    data = load_audit_json(audit_file)
    families = data.get("families", [])
    if not isinstance(families, list):
        raise ValueError(f"{audit_file} has invalid families payload")

    category_templates: List[Tuple[str, Dict[str, object]]] = []
    for family in families:
        if not isinstance(family, dict):
            continue

        category_id = str(family.get("id", ""))
        source_path = str(family.get("sourcePath", category_id))
        if category_filter is not None and category_id != category_filter:
            continue

        items = family.get("items", [])
        if not isinstance(items, list):
            continue

        for item in items:
            if not isinstance(item, dict):
                continue

            if should_include_item(category_id, item):
                category_templates.append(
                    (
                        category_id,
                        map_audit_item_to_template(source_path, category_id, item),
                    )
                )

    grouped = group_templates_by_category(category_templates)
    written_counts: Dict[str, int] = {}
    for category_id, templates in grouped.items():
        target_file = output_root / f"{category_id}.json"
        existing_count = len(load_existing_templates(target_file))
        merge_templates_into_category_file(target_file, templates)
        final_count = len(load_existing_templates(target_file))
        written_counts[category_id] = final_count - existing_count

    return written_counts


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Generate missing static Moongate item templates from a ModernUO audit export.")
    parser.add_argument("audit_json")
    parser.add_argument(
        "--output-root",
        default="moongate_data/templates/items/modernuo",
    )
    parser.add_argument("--category")
    return parser


def main(argv: Optional[List[str]] = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)
    generate_from_audit_file(Path(args.audit_json), Path(args.output_root), args.category)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
