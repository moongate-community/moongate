"""Transform parsed ModernUO NPC data into Moongate template format."""

import re

from .constants import AI_TYPE_TO_BRAIN, LOOT_PACK_MAP, RESISTANCE_TYPE_MAP


def _pascal_to_snake(name: str) -> str:
    """Convert PascalCase to snake_case."""
    s = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", name)
    s = re.sub(r"([a-z\d])([A-Z])", r"\1_\2", s)
    return s.lower()


def _avg(min_val: int, max_val: int) -> int:
    """Return the integer average of two values."""
    return (min_val + max_val) // 2


def _avg_float(min_val: float, max_val: float) -> float:
    """Return the average of two float values."""
    return (min_val + max_val) / 2.0


def _body_hex(body_int: int) -> str:
    """Format body ID as hex string 0xNNNN."""
    return f"0x{body_int:04X}"


def _notoriety_from_karma(karma: int) -> str:
    """Derive notoriety from karma value."""
    if karma < 0:
        return "CanBeAttacked"
    return "Innocent"


def _build_sounds(base_sound_id: int) -> dict:
    """Build sound map from BaseSoundID.

    ModernUO convention:
    - Idle = base
    - StartAttack / Attack = base + 1
    - Attack (hit) = base + 2
    - Defend (hurt) = base + 3
    - Die = base + 4
    """
    return {
        "Idle": base_sound_id,
        "StartAttack": base_sound_id + 1,
        "Attack": base_sound_id + 2,
        "Defend": base_sound_id + 3,
        "Die": base_sound_id + 4,
    }


def _build_skills(skills_raw: dict) -> dict:
    """Convert skill (min, max) pairs to Moongate format (average * 10, as int)."""
    result = {}
    for skill_name, (min_val, max_val) in skills_raw.items():
        avg = _avg_float(min_val, max_val)
        result[skill_name] = int(avg * 10)
    return result


def _build_resistances(resistances_raw: dict) -> dict:
    """Convert resistance (min, max) pairs to Moongate format (average)."""
    result = {}
    for res_type, (min_val, max_val) in resistances_raw.items():
        mapped_key = RESISTANCE_TYPE_MAP.get(res_type, res_type.lower())
        result[mapped_key] = _avg(min_val, max_val)
    return result


def _build_damage_types(damage_types_raw: dict) -> dict:
    """Convert damage type percentages to Moongate format."""
    result = {}
    for dmg_type, value in damage_types_raw.items():
        mapped_key = RESISTANCE_TYPE_MAP.get(dmg_type, dmg_type.lower())
        result[mapped_key] = value
    return result


def _build_loot_tables(parsed: dict) -> list:
    """Build loot table references from parsed loot data."""
    tables = []

    # Add loot pack references
    for loot_entry in parsed.get("loot", []):
        pack_name = loot_entry["pack"]
        count = loot_entry["count"]
        mapped = LOOT_PACK_MAP.get(pack_name)
        if mapped:
            for _ in range(count):
                tables.append(mapped)
        else:
            tables.append(f"loot_pack.{_pascal_to_snake(pack_name)}")

    # If there are pack_items, reference a creature-specific loot table
    if parsed.get("pack_items"):
        class_snake = _pascal_to_snake(parsed["class_name"])
        tables.append(f"creature.{class_snake}")

    return tables


def _build_tags(parsed: dict) -> list:
    """Build tags list from parsed data."""
    tags = []
    category = parsed.get("category", "")

    if category == "monsters":
        tags.append("monster")
    elif category == "animals":
        tags.append("animal")
    elif category == "vendors":
        tags.append("vendor")
        tags.append("npc")
    elif category == "town_npcs":
        tags.append("townfolk")
        tags.append("npc")

    if parsed.get("tamable"):
        tags.append("tamable")

    return tags


def _derive_category_label(parsed: dict) -> str:
    """Derive the category label for the template."""
    category = parsed.get("category", "")
    if category == "monsters":
        return "monster"
    elif category == "animals":
        return "animal"
    elif category in ("vendors", "town_npcs"):
        return "npc"
    return "mobile"


def map_to_template(parsed: dict) -> dict:
    """Convert a parsed NPC dict to a Moongate mobile template dict."""
    class_name = parsed["class_name"]
    template_id = _pascal_to_snake(class_name) + "_npc"

    template = {
        "type": "mobile",
        "id": template_id,
        "category": _derive_category_label(parsed),
        "description": f"Converted from ModernUO {class_name}.",
        "tags": _build_tags(parsed),
    }

    # Body
    if "body" in parsed:
        template["body"] = _body_hex(parsed["body"])

    # Default hue values
    template["skinHue"] = 0
    template["hairHue"] = 0

    # Stats
    if "str_min" in parsed:
        template["strength"] = _avg(parsed["str_min"], parsed["str_max"])
    if "dex_min" in parsed:
        template["dexterity"] = _avg(parsed["dex_min"], parsed["dex_max"])
    if "int_min" in parsed:
        template["intelligence"] = _avg(parsed["int_min"], parsed["int_max"])
    if "hits_min" in parsed:
        template["hits"] = _avg(parsed["hits_min"], parsed["hits_max"])
        template["maxHits"] = _avg(parsed["hits_min"], parsed["hits_max"])
    if "mana_min" in parsed:
        template["mana"] = _avg(parsed["mana_min"], parsed["mana_max"])
    elif "int_min" in parsed:
        # Default mana = intelligence if not explicitly set
        pass
    if "stam_min" in parsed:
        template["stamina"] = _avg(parsed["stam_min"], parsed["stam_max"])

    # Damage
    if "damage_min" in parsed:
        template["minDamage"] = parsed["damage_min"]
        template["maxDamage"] = parsed["damage_max"]

    # Armor
    if "virtual_armor" in parsed:
        template["armorRating"] = parsed["virtual_armor"]

    # Fame / Karma / Notoriety
    if "fame" in parsed:
        template["fame"] = parsed["fame"]
    if "karma" in parsed:
        template["karma"] = parsed["karma"]
        template["notoriety"] = _notoriety_from_karma(parsed["karma"])

    # Brain – use category-aware fallback for NPCs without a mapped AI type
    ai_type = parsed.get("ai_type", "")
    category = parsed.get("category", "")
    if ai_type and ai_type in AI_TYPE_TO_BRAIN:
        brain = AI_TYPE_TO_BRAIN[ai_type]
    elif category in ("vendors", "town_npcs"):
        brain = "vendor"
    else:
        brain = "melee_combat"
    template["brain"] = brain

    # Skills
    if "skills" in parsed:
        template["skills"] = _build_skills(parsed["skills"])

    # Loot tables
    loot_tables = _build_loot_tables(parsed)
    if loot_tables:
        template["lootTables"] = loot_tables

    # Sounds
    if "base_sound_id" in parsed:
        template["sounds"] = _build_sounds(parsed["base_sound_id"])

    # Resistances
    if "resistances" in parsed:
        template["resistances"] = _build_resistances(parsed["resistances"])

    # Damage types
    if "damage_types" in parsed:
        template["damageTypes"] = _build_damage_types(parsed["damage_types"])

    # Equipment placeholders
    template["fixedEquipment"] = []
    template["randomEquipment"] = []

    # Title / Name
    if "title" in parsed:
        template["title"] = parsed["title"]
    elif "name" in parsed:
        template["title"] = parsed["name"]

    return template


def map_pack_items_to_loot(parsed: dict) -> dict | None:
    """Create a creature-specific loot template from PackItem calls.

    Returns None if no pack items exist.
    """
    if not parsed.get("pack_items"):
        return None

    class_name = parsed["class_name"]
    class_snake = _pascal_to_snake(class_name)

    entries = []
    for item in parsed["pack_items"]:
        item_snake = _pascal_to_snake(item["item"])
        entries.append(
            {
                "itemTemplateId": item_snake,
                "chance": 1.0,
                "amount": item["amount"],
            }
        )

    return {
        "type": "loot",
        "id": f"creature.{class_snake}",
        "name": class_name,
        "category": "loot",
        "description": f"Creature-specific loot for {class_name}.",
        "mode": "additive",
        "entries": entries,
    }


def map_vendor_to_sell_profile(parsed: dict) -> dict | None:
    """Create a placeholder sell profile from vendor SBInfo references.

    Returns None if no SBInfo data exists.
    """
    if not parsed.get("sb_infos"):
        return None

    class_name = parsed["class_name"]
    class_snake = _pascal_to_snake(class_name)

    return {
        "type": "sell_profile",
        "id": f"vendor.{class_snake}",
        "name": f"{class_name} Vendor",
        "category": "vendors",
        "description": f"Placeholder sell profile for {class_name}. SBInfo sources: {', '.join(parsed['sb_infos'])}.",
        "vendorItems": [],
        "acceptedItems": [],
    }
