import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_shields_weapons import (
    scan_shield_metadata,
    scan_weapon_metadata,
    sync_shield_template_file,
    sync_weapon_template_file,
)


class ScanShieldMetadataTests(unittest.TestCase):
    def test_extracts_two_handed_layer_strength_and_hit_points(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "WoodenShield.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class WoodenShield : BaseShield
                    {
                        [Constructible]
                        public WoodenShield() : base(0x1B7A)
                        {
                        }

                        public override double DefaultWeight => 5.0;
                        public override int AosStrReq => 20;
                        public override int InitMaxHits => 25;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_shield_metadata(root)

            self.assertEqual("0x1B7A", metadata["wooden_shield"]["itemId"])
            self.assertEqual("TwoHanded", metadata["wooden_shield"]["layer"])
            self.assertEqual(20, metadata["wooden_shield"]["strength"])
            self.assertEqual(25, metadata["wooden_shield"]["hitPoints"])


class ScanWeaponMetadataTests(unittest.TestCase):
    def test_extracts_archery_weapon_metadata_and_two_handed_layer(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "BaseRanged.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseRanged : BaseWeapon
                    {
                        public BaseRanged(int itemID) : base(itemID)
                        {
                        }

                        public override SkillName DefSkill => SkillName.Archery;
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "Bow.cs").write_text(
                """
                namespace Server.Items
                {
                    [Flippable(0x13B2, 0x13B1)]
                    public partial class Bow : BaseRanged
                    {
                        [Constructible]
                        public Bow() : base(0x13B2)
                        {
                            Layer = Layer.TwoHanded;
                        }

                        public override double DefaultWeight => 6.0;
                        public override int EffectID => 0x1BFE;
                        public override Type AmmoType => typeof(Arrow);
                        public override int AosStrReq => 30;
                        public override int AosMinDamage => 15;
                        public override int AosMaxDamage => 19;
                        public override int AosSpeed => 25;
                        public override int OldMinDamage => 9;
                        public override int OldMaxDamage => 41;
                        public override int DefMaxRange => 10;
                        public override int InitMaxHits => 60;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_weapon_metadata(root)
            bow = metadata["bow"]

            self.assertEqual("0x13B2", bow["itemId"])
            self.assertEqual("TwoHanded", bow["layer"])
            self.assertEqual("Archery", bow["weaponSkill"])
            self.assertEqual("0x0F3F", bow["ammo"])
            self.assertEqual("0x1BFE", bow["ammoFx"])
            self.assertEqual(1, bow["baseRange"])
            self.assertEqual(10, bow["maxRange"])
            self.assertEqual(30, bow["strength"])
            self.assertEqual(9, bow["lowDamage"])
            self.assertEqual(41, bow["highDamage"])
            self.assertEqual(25, bow["speed"])
            self.assertEqual(60, bow["hitPoints"])


class SyncShieldTemplateFileTests(unittest.TestCase):
    def test_updates_existing_shield_templates_and_normalizes_script_id(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "shields.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "wooden_shield",
                            "name": "Wooden Shield",
                            "itemId": "0x0001",
                            "scriptId": "items.wooden_shield",
                        }
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_shield_template_file(
                template_file,
                {
                    "wooden_shield": {
                        "itemId": "0x1B7A",
                        "layer": "TwoHanded",
                        "strength": 20,
                        "hitPoints": 25,
                    }
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            self.assertEqual("none", data[0]["scriptId"])
            self.assertEqual("TwoHanded", data[0]["layer"])
            self.assertEqual(20, data[0]["strength"])
            self.assertEqual(25, data[0]["hitPoints"])


class SyncWeaponTemplateFileTests(unittest.TestCase):
    def test_updates_existing_and_appends_missing_weapon_templates(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "weapons.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "bow",
                            "name": "Bow",
                            "category": "Weapons",
                            "description": "Imported from ModernUO (Bow).",
                            "itemId": "0x13B2",
                            "hue": "0",
                            "goldValue": "0",
                            "weight": 6,
                            "scriptId": "items.bow",
                            "isMovable": True,
                            "tags": ["modernuo", "weapons"],
                            "lowDamage": 9,
                            "highDamage": 41,
                            "speed": 25,
                        }
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_weapon_template_file(
                template_file,
                {
                    "bow": {
                        "itemId": "0x13B2",
                        "layer": "TwoHanded",
                        "strength": 30,
                        "dexterity": 0,
                        "intelligence": 0,
                        "hitPoints": 60,
                        "weaponSkill": "Archery",
                        "ammo": "0x0F3F",
                        "ammoFx": "0x1BFE",
                        "baseRange": 1,
                        "maxRange": 10,
                        "lowDamage": 9,
                        "highDamage": 41,
                        "speed": 25,
                        "weight": 6,
                        "name": "Bow",
                        "tags": ["modernuo", "weapons", "flippable"],
                    },
                    "guardian_axe": {
                        "itemId": "0x0F4B",
                        "layer": "TwoHanded",
                        "strength": 40,
                        "dexterity": 0,
                        "intelligence": 0,
                        "hitPoints": 110,
                        "weaponSkill": "Swords",
                        "ammo": None,
                        "ammoFx": None,
                        "baseRange": 1,
                        "maxRange": 1,
                        "lowDamage": 15,
                        "highDamage": 17,
                        "speed": 33,
                        "weight": 6,
                        "name": "Guardian Axe",
                        "tags": ["modernuo", "weapons", "flippable"],
                    },
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            bow = next(item for item in data if item["id"] == "bow")
            guardian_axe = next(item for item in data if item["id"] == "guardian_axe")

            self.assertEqual("", bow["description"])
            self.assertEqual("none", bow["scriptId"])
            self.assertEqual("TwoHanded", bow["layer"])
            self.assertEqual("Archery", bow["weaponSkill"])
            self.assertEqual("0x0F3F", bow["ammo"])
            self.assertEqual("0x1BFE", bow["ammoFx"])
            self.assertEqual("guardian_axe", guardian_axe["id"])
            self.assertEqual("Guardian Axe", guardian_axe["name"])
            self.assertEqual("", guardian_axe["description"])
            self.assertEqual("TwoHanded", guardian_axe["layer"])
            self.assertEqual(["modernuo", "weapons", "flippable"], guardian_axe["tags"])


if __name__ == "__main__":
    unittest.main()
