"""Mapping tables for ModernUO to Moongate conversion."""

AI_TYPE_TO_BRAIN = {
    "AI_Melee": "ai_melee",
    "AI_Archer": "ai_archer",
    "AI_Animal": "ai_animal",
    "AI_Vendor": "ai_vendor",
    "AI_Berserk": "ai_berserk",
    "AI_Mage": "ai_mage",
    "AI_Healer": "ai_healer",
    "AI_Thief": "ai_thief",
    "AI_Predator": "ai_melee",
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
