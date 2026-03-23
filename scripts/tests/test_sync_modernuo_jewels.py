import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_jewels import scan_jewelry_metadata, sync_template_file


class ScanJewelryMetadataTests(unittest.TestCase):
    def test_extracts_ring_bracelet_earrings_and_neck_layers(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "Ring.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseRing : BaseJewel
                    {
                        public BaseRing(int itemID) : base(itemID, Layer.Ring)
                        {
                        }
                    }

                    public partial class GoldRing : BaseRing
                    {
                        [Constructible]
                        public GoldRing() : base(0x108A)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "Bracelet.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseBracelet : BaseJewel
                    {
                        public BaseBracelet(int itemID) : base(itemID, Layer.Bracelet)
                        {
                        }
                    }

                    public partial class SilverBracelet : BaseBracelet
                    {
                        [Constructible]
                        public SilverBracelet() : base(0x1F06)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "Earrings.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseEarrings : BaseJewel
                    {
                        public BaseEarrings(int itemID) : base(itemID, Layer.Earrings)
                        {
                        }
                    }

                    public partial class GoldEarrings : BaseEarrings
                    {
                        [Constructible]
                        public GoldEarrings() : base(0x1087)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "Necklace.cs").write_text(
                """
                namespace Server.Items
                {
                    public abstract partial class BaseNecklace : BaseJewel
                    {
                        public BaseNecklace(int itemID) : base(itemID, Layer.Neck)
                        {
                        }
                    }

                    public partial class Necklace : BaseNecklace
                    {
                        [Constructible]
                        public Necklace() : base(0x1085)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_jewelry_metadata(root)

            self.assertEqual("Ring", metadata["gold_ring"]["layer"])
            self.assertEqual("0x108A", metadata["gold_ring"]["itemId"])
            self.assertEqual("Bracelet", metadata["silver_bracelet"]["layer"])
            self.assertEqual("Earrings", metadata["gold_earrings"]["layer"])
            self.assertEqual("Neck", metadata["necklace"]["layer"])


class SyncJewelryTemplateFileTests(unittest.TestCase):
    def test_updates_existing_templates_in_place_without_rewriting_identity_fields(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "jewels.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "gold_ring",
                            "name": "Gold Ring",
                            "category": "Jewels",
                            "description": "Imported from ModernUO (GoldRing).",
                            "itemId": "0x108A",
                            "scriptId": "none",
                            "tags": ["modernuo", "jewels"],
                        }
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_template_file(
                template_file,
                {
                    "gold_ring": {
                        "itemId": "0x108A",
                        "layer": "Ring",
                        "hitPoints": 0,
                    }
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            self.assertEqual("gold_ring", data[0]["id"])
            self.assertEqual("", data[0]["description"])
            self.assertEqual("none", data[0]["scriptId"])
            self.assertEqual(["modernuo", "jewels"], data[0]["tags"])
            self.assertEqual("Ring", data[0]["layer"])
            self.assertEqual(0, data[0]["hitPoints"])


if __name__ == "__main__":
    unittest.main()
