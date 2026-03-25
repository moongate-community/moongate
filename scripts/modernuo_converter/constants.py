"""Mapping tables for ModernUO to Moongate conversion."""

AI_TYPE_TO_BRAIN = {
    "AI_Melee": "melee_combat",
    "AI_Mage": "mage_combat",
    "AI_Archer": "ranged_combat",
    "AI_Animal": "animal",
    "AI_Vendor": "vendor",
    "AI_Healer": "healer",
    "AI_Berserk": "berserk_combat",
    "AI_Predator": "predator",
    "AI_Thief": "thief",
}

LOOT_PACK_MAP = {
    "Poor": "loot_pack.poor",
    "Meager": "loot_pack.meager",
    "Average": "loot_pack.average",
    "Rich": "loot_pack.rich",
    "FilthyRich": "loot_pack.filthy_rich",
    "UltraRich": "loot_pack.ultra_rich",
    "SuperBoss": "loot_pack.super_boss",
    "LowScrolls": "loot_pack.low_scrolls",
    "MedScrolls": "loot_pack.med_scrolls",
    "HighScrolls": "loot_pack.high_scrolls",
    "Gems": "loot_pack.gems",
    "Potions": "loot_pack.potions",
}

CATEGORY_PATHS = {
    "monsters": "Projects/UOContent/Mobiles/Monsters",
    "animals": "Projects/UOContent/Mobiles/Animals",
    "vendors": "Projects/UOContent/Mobiles/Vendors/NPC",
    "town_npcs": "Projects/UOContent/Mobiles/Townfolk",
}

RESISTANCE_TYPE_MAP = {
    "Physical": "physical",
    "Fire": "fire",
    "Cold": "cold",
    "Poison": "poison",
    "Energy": "energy",
}
