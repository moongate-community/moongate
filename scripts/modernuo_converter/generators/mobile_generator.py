"""Generate Moongate mobile template JSON files."""

import json
import os
from typing import Optional


def _subfolder_name(parsed: dict) -> str:
    """Determine the output subfolder based on category and subcategory."""
    category = parsed.get("category", "misc")
    subcategory = parsed.get("subcategory", "")

    if category == "monsters":
        if subcategory:
            # Flatten path separators to underscores for cleaner folder names
            return os.path.join("monsters", subcategory.replace(os.sep, "/").replace("/", "_").lower())
        return "monsters"
    elif category == "animals":
        if subcategory:
            return os.path.join("animals", subcategory.replace(os.sep, "/").replace("/", "_").lower())
        return "animals"
    elif category == "vendors":
        return "vendors"
    elif category == "town_npcs":
        return "townfolk"
    return "misc"


def generate_mobile_file(
    template: dict,
    parsed: dict,
    output_dir: str,
    dry_run: bool = False,
) -> Optional[str]:
    """Write a single mobile template JSON file.

    Each file contains a JSON array with one template object, matching the
    existing Moongate pattern (e.g., guards.json, undead.json).

    Returns the output file path, or None on dry-run.
    """
    template_id = template["id"]
    subfolder = _subfolder_name(parsed)
    out_dir = os.path.join(output_dir, "templates", "mobiles", subfolder)
    out_file = os.path.join(out_dir, f"{template_id}.json")

    if dry_run:
        return out_file

    os.makedirs(out_dir, exist_ok=True)
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump([template], f, indent=2, ensure_ascii=False)
        f.write("\n")

    return out_file


def generate_all_mobiles(
    templates: list[tuple[dict, dict]],
    output_dir: str,
    dry_run: bool = False,
) -> list[str]:
    """Generate mobile JSON files for all templates.

    Args:
        templates: list of (template_dict, parsed_dict) tuples
        output_dir: base output directory
        dry_run: if True, do not write files

    Returns list of output file paths.
    """
    paths = []
    for template, parsed in templates:
        path = generate_mobile_file(template, parsed, output_dir, dry_run)
        if path:
            paths.append(path)
    return paths
