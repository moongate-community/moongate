"""Generate Moongate loot template JSON files."""

import json
import os
from typing import Optional


def generate_creature_loot_file(
    loot_template: dict,
    output_dir: str,
    dry_run: bool = False,
) -> Optional[str]:
    """Write a creature-specific loot template JSON file.

    Returns the output file path, or None on dry-run.
    """
    loot_id = loot_template["id"]
    out_dir = os.path.join(output_dir, "templates", "loot", "creatures")
    # File name from the loot id: "creature.drake" -> "creature_drake.json"
    filename = loot_id.replace(".", "_") + ".json"
    out_file = os.path.join(out_dir, filename)

    if dry_run:
        return out_file

    os.makedirs(out_dir, exist_ok=True)
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump([loot_template], f, indent=2, ensure_ascii=False)
        f.write("\n")

    return out_file


def generate_all_loot(
    loot_templates: list[dict],
    output_dir: str,
    dry_run: bool = False,
) -> list[str]:
    """Generate loot JSON files for all creature-specific loot templates.

    Returns list of output file paths.
    """
    paths = []
    for loot in loot_templates:
        path = generate_creature_loot_file(loot, output_dir, dry_run)
        if path:
            paths.append(path)
    return paths
