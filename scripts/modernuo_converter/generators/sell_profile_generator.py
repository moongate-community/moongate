"""Generate Moongate sell profile JSON files."""

import json
import os
from typing import Optional


def generate_sell_profile_file(
    profile: dict,
    output_dir: str,
    dry_run: bool = False,
) -> Optional[str]:
    """Write a sell profile JSON file.

    Skips writing if the file already exists (to avoid overwriting
    hand-crafted profiles).

    Returns the output file path if written, or None if skipped/dry-run.
    """
    profile_id = profile["id"]
    out_dir = os.path.join(output_dir, "templates", "sell_profiles")
    # "vendor.blacksmith" -> "vendor_blacksmith.json"
    filename = profile_id.replace(".", "_") + ".json"
    out_file = os.path.join(out_dir, filename)

    # Do not overwrite existing sell profiles
    if os.path.exists(out_file):
        return None

    if dry_run:
        return out_file

    os.makedirs(out_dir, exist_ok=True)
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump([profile], f, indent=2, ensure_ascii=False)
        f.write("\n")

    return out_file


def generate_all_sell_profiles(
    profiles: list[dict],
    output_dir: str,
    dry_run: bool = False,
) -> list[str]:
    """Generate sell profile JSON files, skipping those that already exist.

    Returns list of output file paths that were (or would be) written.
    """
    paths = []
    for profile in profiles:
        path = generate_sell_profile_file(profile, output_dir, dry_run)
        if path:
            paths.append(path)
    return paths
