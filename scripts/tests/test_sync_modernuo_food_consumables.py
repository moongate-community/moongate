import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_food_consumables import (
    build_beverage_templates,
    scan_food_metadata,
    sync_template_file,
)


class ScanFoodMetadataTests(unittest.TestCase):
    def test_imports_edible_food_and_skips_cookable_food(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir) / "Food"
            root.mkdir()
            (root / "Food.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class Item
                    {
                    }

                    public abstract partial class Food : Item
                    {
                    }

                    public abstract partial class CookableFood : Item
                    {
                    }

                    public partial class Apple : Food
                    {
                        [Constructible]
                        public Apple(int amount = 1) : base(0x09D0, amount)
                        {
                        }

                        public override double DefaultWeight => 1.0;
                    }

                    public partial class CakeMix : CookableFood
                    {
                        [Constructible]
                        public CakeMix() : base(0x103F)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_food_metadata(root)

            self.assertIn("apple", metadata)
            self.assertNotIn("cake_mix", metadata)
            self.assertEqual("0x09D0", metadata["apple"]["itemId"])
            self.assertEqual(1, metadata["apple"]["weight"])


class BuildBeverageTemplatesTests(unittest.TestCase):
    def test_builds_alias_based_beverage_templates(self) -> None:
        templates = build_beverage_templates()

        self.assertIn("bottle_ale", templates)
        self.assertIn("pitcher_water", templates)
        self.assertEqual("0x099F", templates["bottle_ale"]["itemId"])
        self.assertEqual("items.beverage", templates["pitcher_water"]["scriptId"])


class SyncFoodTemplateFileTests(unittest.TestCase):
    def test_updates_food_scripts_and_appends_beverages(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "food.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "apple",
                            "name": "Apple",
                            "category": "Food",
                            "description": "Imported from ModernUO (Apple).",
                            "itemId": "0x09D0",
                            "scriptId": "none",
                            "tags": ["modernuo", "food"],
                        },
                        {
                            "type": "item",
                            "id": "cake_mix",
                            "name": "Cake Mix",
                            "category": "Food",
                            "description": "Imported from ModernUO (CakeMix).",
                            "itemId": "0x103F",
                            "scriptId": "none",
                            "tags": ["modernuo", "food"],
                        },
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_template_file(
                template_file,
                {
                    "apple": {
                        "itemId": "0x09D0",
                        "weight": 1,
                    }
                },
                {
                    "bottle_ale": {
                        "type": "item",
                        "id": "bottle_ale",
                        "name": "Bottle Of Ale",
                        "category": "Food",
                        "description": "",
                        "itemId": "0x099F",
                        "hue": "0",
                        "goldValue": "0",
                        "weight": 1,
                        "scriptId": "items.beverage",
                        "isMovable": True,
                        "tags": ["modernuo", "food", "beverage"],
                    }
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            apple = next(item for item in data if item["id"] == "apple")
            cake_mix = next(item for item in data if item["id"] == "cake_mix")
            bottle_ale = next(item for item in data if item["id"] == "bottle_ale")

            self.assertEqual("", apple["description"])
            self.assertEqual("items.food", apple["scriptId"])
            self.assertEqual("", cake_mix["description"])
            self.assertEqual("none", cake_mix["scriptId"])
            self.assertEqual("", bottle_ale["description"])
            self.assertEqual("items.beverage", bottle_ale["scriptId"])
            self.assertEqual("0x099F", bottle_ale["itemId"])


if __name__ == "__main__":
    unittest.main()
