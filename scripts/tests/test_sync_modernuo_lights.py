import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_lights import scan_light_metadata, sync_template_file


class ScanLightMetadataTests(unittest.TestCase):
    def test_extracts_toggle_pair_and_default_sounds_for_candle(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "Candle.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class Candle : BaseEquipableLight
                    {
                        [Constructible]
                        public Candle() : base(0xA28)
                        {
                        }

                        public override int LitItemID => 0xA0F;
                        public override int UnlitItemID => 0xA28;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_light_metadata(root)
            candle = metadata["candle"]

            self.assertEqual("0x0A0F", candle["litItemId"])
            self.assertEqual("0x0A28", candle["unlitItemId"])
            self.assertEqual("0x0047", candle["toggleSoundOn"])
            self.assertEqual("0x03BE", candle["toggleSoundOff"])
            self.assertEqual("TwoHanded", candle["layer"])

    def test_keeps_brazier_unscripted_when_no_unlit_pair_exists(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "Brazier.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class Brazier : BaseLight
                    {
                        [Constructible]
                        public Brazier() : base(0x0E31)
                        {
                        }

                        public override int LitItemID => 0x0E31;
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_light_metadata(root)
            self.assertFalse(metadata["brazier"]["toggleable"])


class SyncLightTemplateFileTests(unittest.TestCase):
    def test_updates_toggleable_light_and_leaves_always_on_light_unscripted(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "lights.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "candle",
                            "name": "Candle",
                            "category": "Lights",
                            "description": "Imported from ModernUO (Candle).",
                            "itemId": "0x0A28",
                            "scriptId": "none",
                        },
                        {
                            "type": "item",
                            "id": "brazier",
                            "name": "Brazier",
                            "category": "Lights",
                            "description": "Imported from ModernUO (Brazier).",
                            "itemId": "0x0E31",
                            "scriptId": "none",
                        },
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_template_file(
                template_file,
                {
                    "candle": {
                        "toggleable": True,
                        "litItemId": "0x0A0F",
                        "unlitItemId": "0x0A28",
                        "burning": "false",
                        "toggleSoundOn": "0x0047",
                        "toggleSoundOff": "0x03BE",
                        "layer": "TwoHanded",
                    },
                    "brazier": {
                        "toggleable": False,
                        "litItemId": "0x0E31",
                        "unlitItemId": None,
                        "burning": "true",
                        "toggleSoundOn": "0x0047",
                        "toggleSoundOff": "0x03BE",
                        "layer": None,
                    },
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            candle = next(item for item in data if item["id"] == "candle")
            brazier = next(item for item in data if item["id"] == "brazier")

            self.assertEqual("", candle["description"])
            self.assertEqual("items.light_source", candle["scriptId"])
            self.assertEqual("TwoHanded", candle["layer"])
            self.assertEqual("0x0A0F", candle["params"]["light_lit_item_id"]["value"])
            self.assertEqual("false", candle["params"]["light_burning"]["value"])
            self.assertEqual("", brazier["description"])
            self.assertEqual("none", brazier["scriptId"])
            self.assertNotIn("params", brazier)


if __name__ == "__main__":
    unittest.main()
