import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_wearable_metadata import (
    scan_armor_metadata,
    scan_clothing_metadata,
    sync_template_file,
)


class ScanClothingMetadataTests(unittest.TestCase):
    def test_extracts_layer_and_strength_from_base_clothing_hierarchy(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "Hats.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseHat : BaseClothing
                    {
                        public BaseHat(int itemID, int hue = 0) : base(itemID, Layer.Helm, hue)
                        {
                        }
                    }

                    public partial class Bandana : BaseHat
                    {
                        [Constructible]
                        public Bandana(int hue = 0) : base(0x1540, hue)
                        {
                        }

                        public override int AosStrReq => 15;
                        public override int InitMaxHits => 30;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_clothing_metadata(root)

            self.assertEqual("Helm", metadata["bandana"]["layer"])
            self.assertEqual("0x1540", metadata["bandana"]["itemId"])
            self.assertEqual(15, metadata["bandana"]["strength"])
            self.assertEqual(0, metadata["bandana"]["dexterity"])
            self.assertEqual(0, metadata["bandana"]["intelligence"])
            self.assertEqual(30, metadata["bandana"]["hitPoints"])

    def test_extracts_explicit_layer_override_from_clothing_constructor(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "Shirts.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseShirt : BaseClothing
                    {
                        public BaseShirt(int itemID, int hue = 0) : base(itemID, Layer.Shirt, hue)
                        {
                        }
                    }

                    public partial class ClothNinjaJacket : BaseShirt
                    {
                        [Constructible]
                        public ClothNinjaJacket(int hue = 0) : base(0x2794, hue) => Layer = Layer.InnerTorso;

                        public override int InitMaxHits => 40;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_clothing_metadata(root)

            self.assertEqual("InnerTorso", metadata["cloth_ninja_jacket"]["layer"])
            self.assertEqual(40, metadata["cloth_ninja_jacket"]["hitPoints"])


class ScanArmorMetadataTests(unittest.TestCase):
    def test_extracts_layer_and_requirements_from_armor_class_name_and_overrides(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "BoneArms.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class BoneArms : BaseArmor
                    {
                        [Constructible]
                        public BoneArms() : base(0x144E)
                        {
                        }

                        public override int AosStrReq => 55;
                        public override int AosDexReq => 12;
                        public override int AosIntReq => 3;
                        public override int InitMaxHits => 30;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_armor_metadata(root)

            self.assertEqual("Arms", metadata["bone_arms"]["layer"])
            self.assertEqual("0x144E", metadata["bone_arms"]["itemId"])
            self.assertEqual(55, metadata["bone_arms"]["strength"])
            self.assertEqual(12, metadata["bone_arms"]["dexterity"])
            self.assertEqual(3, metadata["bone_arms"]["intelligence"])
            self.assertEqual(30, metadata["bone_arms"]["hitPoints"])

    def test_extracts_inner_legs_layer_for_haidate_armor(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "PlateHaidate.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class PlateHaidate : BaseArmor
                    {
                        [Constructible]
                        public PlateHaidate() : base(0x278D)
                        {
                        }

                        public override int AosStrReq => 25;
                        public override int InitMaxHits => 55;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_armor_metadata(root)

            self.assertEqual("InnerLegs", metadata["plate_haidate"]["layer"])


class SyncTemplateFileTests(unittest.TestCase):
    def test_updates_existing_templates_and_normalizes_script_id(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "armor.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "bone_arms",
                            "name": "Bone Arms",
                            "description": "Imported from ModernUO (BoneArms).",
                            "itemId": "0x144E",
                            "scriptId": "items.bone_arms",
                        }
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_template_file(
                template_file,
                {
                    "bone_arms": {
                        "itemId": "0x144E",
                        "layer": "Arms",
                        "strength": 55,
                        "dexterity": 0,
                        "intelligence": 0,
                        "hitPoints": 30,
                    }
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            self.assertEqual("none", data[0]["scriptId"])
            self.assertEqual("", data[0]["description"])
            self.assertEqual("Arms", data[0]["layer"])
            self.assertEqual(55, data[0]["strength"])
            self.assertEqual(30, data[0]["hitPoints"])


if __name__ == "__main__":
    unittest.main()
