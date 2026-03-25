"""Regex-based parser for ModernUO C# NPC source files."""

import os
import re
from typing import Optional


def parse_file(filepath: str) -> Optional[dict]:
    """Parse a single ModernUO C# NPC source file and extract relevant data.

    Returns None for abstract classes, Base* classes, or files without constructors.
    """
    with open(filepath, "r", encoding="utf-8-sig") as f:
        content = f.read()

    # Skip abstract classes
    if re.search(r"\babstract\s+(?:partial\s+)?class\b", content):
        return None

    # Extract class name and base class
    class_match = re.search(
        r"public\s+(?:partial\s+)?class\s+(\w+)\s*:\s*(\w+)", content
    )
    if not class_match:
        return None

    class_name = class_match.group(1)
    base_class = class_match.group(2)

    # Skip Base* classes (e.g., BaseCreature, BaseVendor)
    if class_name.startswith("Base"):
        return None

    # Must have [Constructible] or [Constructable] attribute to indicate it's instantiable
    if "[Constructible]" not in content and "[Constructable]" not in content:
        return None

    data = {
        "class_name": class_name,
        "base_class": base_class,
        "source_file": filepath,
    }

    # Extract AIType from base constructor call
    ai_match = re.search(r"base\s*\(\s*AIType\.(\w+)", content)
    if ai_match:
        data["ai_type"] = ai_match.group(1)

    # Extract Body
    body_match = re.search(r"\bBody\s*=\s*(?:Utility\.RandomList\s*\(\s*)?(\d+|0x[0-9A-Fa-f]+)", content)
    if body_match:
        val = body_match.group(1)
        data["body"] = int(val, 16) if val.startswith("0x") else int(val)

    # Extract DefaultName
    name_match = re.search(r'DefaultName\s*=>\s*"([^"]+)"', content)
    if name_match:
        data["name"] = name_match.group(1)
    else:
        # Try Name = assignment in constructor
        name_assign = re.search(r'\bName\s*=\s*(?:NameList\.RandomName\s*\(\s*"(\w+)"\s*\)|"([^"]+)")', content)
        if name_assign:
            data["name"] = name_assign.group(1) or name_assign.group(2)

    # Extract Title from base("the something") for vendors
    title_match = re.search(r'base\s*\(\s*"([^"]+)"', content)
    if title_match:
        data["title"] = title_match.group(1)

    # Extract BaseSoundID
    sound_match = re.search(r"BaseSoundID\s*=\s*(\d+|0x[0-9A-Fa-f]+)", content)
    if sound_match:
        val = sound_match.group(1)
        data["base_sound_id"] = int(val, 16) if val.startswith("0x") else int(val)

    # Extract stats: SetStr, SetDex, SetInt, SetHits, SetStam, SetMana, SetDamage
    for stat_name, key in [
        ("SetStr", "str"),
        ("SetDex", "dex"),
        ("SetInt", "int"),
        ("SetHits", "hits"),
        ("SetStam", "stam"),
        ("SetMana", "mana"),
        ("SetDamage", "damage"),
    ]:
        stat_match = re.search(
            rf"{stat_name}\s*\(\s*(\d+)\s*(?:,\s*(\d+))?\s*\)", content
        )
        if stat_match:
            min_val = int(stat_match.group(1))
            max_val = int(stat_match.group(2)) if stat_match.group(2) else min_val
            data[f"{key}_min"] = min_val
            data[f"{key}_max"] = max_val

    # Extract resistances: SetResistance(ResistanceType.X, min, max) or SetResistance(ResistanceType.X, val)
    resistances = {}
    for res_match in re.finditer(
        r"SetResistance\s*\(\s*ResistanceType\.(\w+)\s*,\s*(\d+)\s*(?:,\s*(\d+))?\s*\)",
        content,
    ):
        res_type = res_match.group(1)
        res_min = int(res_match.group(2))
        res_max = int(res_match.group(3)) if res_match.group(3) else res_min
        resistances[res_type] = (res_min, res_max)
    if resistances:
        data["resistances"] = resistances

    # Extract damage types: SetDamageType(ResistanceType.X, val)
    damage_types = {}
    for dmg_match in re.finditer(
        r"SetDamageType\s*\(\s*ResistanceType\.(\w+)\s*,\s*(\d+)\s*\)", content
    ):
        dmg_type = dmg_match.group(1)
        dmg_val = int(dmg_match.group(2))
        damage_types[dmg_type] = dmg_val
    if damage_types:
        data["damage_types"] = damage_types

    # Extract skills: SetSkill(SkillName.X, min, max)
    skills = {}
    for skill_match in re.finditer(
        r"SetSkill\s*\(\s*SkillName\.(\w+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*\)",
        content,
    ):
        skill_name = skill_match.group(1)
        skill_min = float(skill_match.group(2))
        skill_max = float(skill_match.group(3))
        skills[skill_name] = (skill_min, skill_max)

    # Also check for Skills.X.Base = N pattern
    for base_skill_match in re.finditer(
        r"Skills\.(\w+)\.Base\s*=\s*([\d.]+)", content
    ):
        skill_name = base_skill_match.group(1)
        val = float(base_skill_match.group(2))
        skills[skill_name] = (val, val)

    if skills:
        data["skills"] = skills

    # Extract properties
    fame_match = re.search(r"Fame\s*=\s*(\d+)", content)
    if fame_match:
        data["fame"] = int(fame_match.group(1))

    karma_match = re.search(r"Karma\s*=\s*(-?\d+)", content)
    if karma_match:
        data["karma"] = int(karma_match.group(1))

    va_match = re.search(r"VirtualArmor\s*=\s*(\d+)", content)
    if va_match:
        data["virtual_armor"] = int(va_match.group(1))

    # Tamable properties
    if re.search(r"Tamable\s*=\s*true", content, re.IGNORECASE):
        data["tamable"] = True

    tame_skill_match = re.search(r"MinTameSkill\s*=\s*([\d.]+)", content)
    if tame_skill_match:
        data["min_tame_skill"] = float(tame_skill_match.group(1))

    control_match = re.search(r"ControlSlots\s*=\s*(\d+)", content)
    if control_match:
        data["control_slots"] = int(control_match.group(1))

    # Extract loot: AddLoot(LootPack.X) and AddLoot(LootPack.X, count)
    loot_entries = []
    for loot_match in re.finditer(
        r"AddLoot\s*\(\s*LootPack\.(\w+)(?:\s*,\s*(\d+))?\s*\)", content
    ):
        pack_name = loot_match.group(1)
        count = int(loot_match.group(2)) if loot_match.group(2) else 1
        loot_entries.append({"pack": pack_name, "count": count})
    if loot_entries:
        data["loot"] = loot_entries

    # Extract PackItem calls: PackItem(new X(N)) or PackItem(new X())
    pack_items = []
    for pack_match in re.finditer(
        r"PackItem\s*\(\s*new\s+(\w+)\s*\(\s*(\d+)?\s*\)\s*\)", content
    ):
        item_name = pack_match.group(1)
        amount = int(pack_match.group(2)) if pack_match.group(2) else 1
        pack_items.append({"item": item_name, "amount": amount})
    # Also match PackReg, PackGold, etc.
    for pack_call in re.finditer(r"PackReg\s*\(\s*(\d+)\s*\)", content):
        pack_items.append({"item": "Reagent", "amount": int(pack_call.group(1))})
    for pack_call in re.finditer(
        r"PackGold\s*\(\s*(\d+)\s*(?:,\s*(\d+))?\s*\)", content
    ):
        min_gold = int(pack_call.group(1))
        max_gold = int(pack_call.group(2)) if pack_call.group(2) else min_gold
        pack_items.append(
            {"item": "Gold", "amount": (min_gold + max_gold) // 2}
        )
    if pack_items:
        data["pack_items"] = pack_items

    # Extract vendor SBInfo: m_SBInfos.Add(new SBX())
    sb_infos = []
    for sb_match in re.finditer(r"m_SBInfos\.Add\s*\(\s*new\s+(\w+)\s*\(\s*\)\s*\)", content):
        sb_infos.append(sb_match.group(1))
    if sb_infos:
        data["sb_infos"] = sb_infos

    return data


def parse_directory(source_path: str, category: str, category_path: str) -> list:
    """Parse all C# files in a category directory.

    Returns a list of parsed NPC data dicts, each annotated with category info.
    """
    full_path = os.path.join(source_path, category_path)
    if not os.path.isdir(full_path):
        print(f"  Warning: directory not found: {full_path}")
        return []

    results = []
    for root, _dirs, files in os.walk(full_path):
        for filename in sorted(files):
            if not filename.endswith(".cs"):
                continue
            filepath = os.path.join(root, filename)
            try:
                parsed = parse_file(filepath)
                if parsed:
                    # Compute subcategory from relative path
                    rel_path = os.path.relpath(root, full_path)
                    parsed["category"] = category
                    parsed["subcategory"] = (
                        rel_path if rel_path != "." else ""
                    )
                    results.append(parsed)
            except Exception as e:
                print(f"  Error parsing {filepath}: {e}")

    return results
