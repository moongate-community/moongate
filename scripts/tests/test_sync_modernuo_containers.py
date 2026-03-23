import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_containers import scan_container_metadata, sync_template_file


class ScanContainerMetadataTests(unittest.TestCase):
    def test_imports_normal_containers_and_excludes_special_types(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir) / "Containers"
            root.mkdir()
            (root / "Container.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class Container
                    {
                    }

                    public abstract partial class BaseContainer : Container
                    {
                    }

                    public abstract partial class TrappableContainer : BaseContainer
                    {
                    }

                    public abstract partial class LockableContainer : TrappableContainer
                    {
                    }

                    [Flippable(0x0E75, 0x0E79)]
                    public partial class Backpack : BaseContainer
                    {
                        [Constructible]
                        public Backpack() : base(0x0E75)
                        {
                        }

                        public override double DefaultWeight => 3.0;
                    }

                    public partial class Barrel : BaseContainer
                    {
                        [Constructible]
                        public Barrel() : base(0x0E77)
                        {
                        }

                        public override double DefaultWeight => 1.0;
                    }

                    public partial class Pouch : TrappableContainer
                    {
                        [Constructible]
                        public Pouch() : base(0x0E79)
                        {
                        }
                    }

                    public partial class WoodenBox : LockableContainer
                    {
                        [Constructible]
                        public WoodenBox() : base(0x09AA)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "FurnitureContainer.cs").write_text(
                """
                namespace Server.Items
                {
                    [Flippable(0x2815, 0x2816)]
                    public partial class TallCabinet : BaseContainer
                    {
                        [Constructible]
                        public TallCabinet() : base(0x2815)
                        {
                        }

                        public override double DefaultWeight => 1.0;
                    }
                }
                """,
                encoding="utf-8",
            )

            gump_file = Path(tmp_dir) / "containers.cfg"
            gump_file.write_text(
                """
                0x3D\t29 34 108 94\t0x48\t0xE75
                0x3E\t33 36 109 112\t0x42\t0xE77
                0x10C\t10 10 150 95\t0x42\t0x2815,0x2816
                """.strip()
                + "\n",
                encoding="utf-8",
            )

            layouts_file = Path(tmp_dir) / "default_containers.json"
            layouts_file.write_text(
                json.dumps(
                    [
                        {"Id": "backpack", "ItemId": 0x0E75, "Width": 7, "Height": 4, "Name": "Backpack"},
                        {"Id": "barrel", "ItemId": 0x0E77, "Width": 6, "Height": 6, "Name": "Barrel"},
                    ]
                ),
                encoding="utf-8",
            )

            metadata = scan_container_metadata(root, gump_file, layouts_file)

            self.assertIn("backpack", metadata)
            self.assertIn("barrel", metadata)
            self.assertIn("tall_cabinet", metadata)
            self.assertNotIn("pouch", metadata)
            self.assertNotIn("wooden_box", metadata)
            self.assertEqual("0x003D", metadata["backpack"]["gumpId"])
            self.assertEqual("backpack", metadata["backpack"]["containerLayoutId"])
            self.assertEqual(125, metadata["backpack"]["maxItems"])
            self.assertEqual(40000, metadata["backpack"]["weightMax"])
            self.assertEqual(["0x0E75", "0x0E79"], metadata["backpack"]["flippableItemIds"])


class SyncContainerTemplateFileTests(unittest.TestCase):
    def test_updates_allowed_containers_removes_excluded_and_appends_missing(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "containers.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "backpack",
                            "name": "Backpack",
                            "category": "Containers",
                            "description": "Imported from ModernUO (Backpack).",
                            "itemId": "0x0E75",
                            "scriptId": "none",
                            "tags": ["modernuo", "containers"],
                        },
                        {
                            "type": "item",
                            "id": "pouch",
                            "name": "Pouch",
                            "category": "Containers",
                            "description": "Imported from ModernUO (Pouch).",
                            "itemId": "0x0E79",
                            "scriptId": "none",
                            "tags": ["modernuo", "containers"],
                        },
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_template_file(
                template_file,
                {
                    "backpack": {
                        "name": "Backpack",
                        "itemId": "0x0E75",
                        "weight": 3,
                        "gumpId": "0x003D",
                        "containerLayoutId": "backpack",
                        "weightMax": 40000,
                        "maxItems": 125,
                        "flippableItemIds": ["0x0E75", "0x0E79"],
                    },
                    "barrel": {
                        "name": "Barrel",
                        "itemId": "0x0E77",
                        "weight": 1,
                        "gumpId": "0x003E",
                        "containerLayoutId": "barrel",
                        "weightMax": 40000,
                        "maxItems": 125,
                        "flippableItemIds": [],
                    },
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            ids = [item["id"] for item in data]
            backpack = next(item for item in data if item["id"] == "backpack")
            barrel = next(item for item in data if item["id"] == "barrel")

            self.assertEqual(["backpack", "barrel"], ids)
            self.assertEqual("", backpack["description"])
            self.assertEqual("0x003D", backpack["gumpId"])
            self.assertEqual("backpack", backpack["containerLayoutId"])
            self.assertEqual(125, backpack["maxItems"])
            self.assertEqual(40000, backpack["weightMax"])
            self.assertEqual(["0x0E75", "0x0E79"], backpack["flippableItemIds"])
            self.assertEqual("none", backpack["scriptId"])
            self.assertEqual("", barrel["description"])
            self.assertEqual("0x003E", barrel["gumpId"])
            self.assertEqual("barrel", barrel["containerLayoutId"])
            self.assertEqual("none", barrel["scriptId"])


if __name__ == "__main__":
    unittest.main()
