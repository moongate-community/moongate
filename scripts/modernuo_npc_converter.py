#!/usr/bin/env python3
"""CLI entry point for the ModernUO NPC to Moongate template converter.

Parses ModernUO C# NPC source files and generates Moongate-compatible JSON
templates for mobiles, loot, and sell profiles.

Usage:
    python3 scripts/modernuo_npc_converter.py --monsters --dry-run
    python3 scripts/modernuo_npc_converter.py --all --source /path/to/ModernUO
    python3 scripts/modernuo_npc_converter.py --animals --vendors --output ./out
"""

import argparse
import sys
from pathlib import Path

from modernuo_converter.constants import CATEGORY_PATHS
from modernuo_converter.mapper import (
    map_pack_items_to_loot,
    load_item_template_catalog,
    map_to_template,
    map_vendor_to_sell_profile,
)
from modernuo_converter.parser import parse_directory
from modernuo_converter.generators.mobile_generator import generate_all_mobiles
from modernuo_converter.generators.loot_generator import generate_all_loot
from modernuo_converter.generators.sell_profile_generator import (
    generate_all_sell_profiles,
)

MANUALLY_MANAGED_MOBILE_TEMPLATE_IDS = {
    "skeletal_knight_npc",
    "zombie_npc",
}


def main():
    parser = argparse.ArgumentParser(
        description="Convert ModernUO NPC C# sources to Moongate JSON templates."
    )
    parser.add_argument(
        "--source",
        default=None,
        help="Path to the ModernUO repository root (required).",
    )
    parser.add_argument(
        "--output",
        default="moongate_data",
        help="Output directory for generated templates.",
    )
    parser.add_argument(
        "--monsters",
        action="store_true",
        help="Process monster NPCs.",
    )
    parser.add_argument(
        "--animals",
        action="store_true",
        help="Process animal NPCs.",
    )
    parser.add_argument(
        "--vendors",
        action="store_true",
        help="Process vendor NPCs.",
    )
    parser.add_argument(
        "--town-npcs",
        action="store_true",
        help="Process townfolk NPCs.",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Process all NPC categories.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Parse and map only; do not write output files.",
    )

    args = parser.parse_args()

    # Determine which categories to process
    categories = []
    if args.all:
        categories = list(CATEGORY_PATHS.keys())
    else:
        if args.monsters:
            categories.append("monsters")
        if args.animals:
            categories.append("animals")
        if args.vendors:
            categories.append("vendors")
        if args.town_npcs:
            categories.append("town_npcs")

    if not args.source:
        print("Error: --source is required. Provide the path to the ModernUO repository root.")
        sys.exit(1)

    if not categories:
        print("Error: specify at least one category (--monsters, --animals, --vendors, --town-npcs) or --all.")
        sys.exit(1)

    print(f"Source: {args.source}")
    print(f"Output: {args.output}")
    print(f"Categories: {', '.join(categories)}")
    print(f"Dry run: {args.dry_run}")
    print()

    # Phase 1: Parse
    all_parsed = []
    for category in categories:
        cat_path = CATEGORY_PATHS[category]
        print(f"Parsing {category} from {cat_path}...")
        parsed_list = parse_directory(args.source, category, cat_path)
        print(f"  Found {len(parsed_list)} NPCs")
        all_parsed.extend(parsed_list)

    print(f"\nTotal parsed: {len(all_parsed)} NPCs")

    # Phase 2: Map
    item_catalog = load_item_template_catalog(Path(args.output) / "templates" / "items")
    mobile_templates = []  # (template, parsed) tuples
    loot_templates = []
    sell_profiles = []

    for parsed in all_parsed:
        template = map_to_template(parsed, item_catalog=item_catalog)
        if template["id"] not in MANUALLY_MANAGED_MOBILE_TEMPLATE_IDS:
            mobile_templates.append((template, parsed))

        loot = map_pack_items_to_loot(parsed)
        if loot:
            loot_templates.append(loot)

        profile = map_vendor_to_sell_profile(parsed)
        if profile:
            sell_profiles.append(profile)

    print(f"Mapped: {len(mobile_templates)} mobiles, {len(loot_templates)} creature loot tables, {len(sell_profiles)} sell profiles")

    # Phase 3: Generate
    if args.dry_run:
        print("\n[DRY RUN] Would generate:")
        mobile_paths = generate_all_mobiles(mobile_templates, args.output, dry_run=True)
        loot_paths = generate_all_loot(loot_templates, args.output, dry_run=True)
        profile_paths = generate_all_sell_profiles(sell_profiles, args.output, dry_run=True)

        print(f"  {len(mobile_paths)} mobile files")
        print(f"  {len(loot_paths)} creature loot files")
        print(f"  {len(profile_paths)} sell profile files")

        if mobile_paths:
            print("\nSample mobile paths:")
            for p in mobile_paths[:5]:
                print(f"  {p}")
            if len(mobile_paths) > 5:
                print(f"  ... and {len(mobile_paths) - 5} more")

        if loot_paths:
            print("\nSample loot paths:")
            for p in loot_paths[:5]:
                print(f"  {p}")

        if profile_paths:
            print("\nSample sell profile paths:")
            for p in profile_paths[:5]:
                print(f"  {p}")
    else:
        print("\nGenerating files...")
        mobile_paths = generate_all_mobiles(mobile_templates, args.output)
        loot_paths = generate_all_loot(loot_templates, args.output)
        profile_paths = generate_all_sell_profiles(sell_profiles, args.output)

        print(f"  Written {len(mobile_paths)} mobile files")
        print(f"  Written {len(loot_paths)} creature loot files")
        print(f"  Written {len(profile_paths)} sell profile files")

    print("\nDone.")


if __name__ == "__main__":
    main()
