import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from scripts.modernuo_item_template_tooling import (
    MODERNUO_ITEMS_ROOT,
    ROOT_ITEMS_DIRECTORY,
    PRESERVED_SCRIPT_IDS,
    migrate_modernuo_item_templates,
    normalize_script_id,
)


class NormalizeScriptIdTests(unittest.TestCase):
    def test_defaults_to_none_for_non_allowlisted_item_script(self) -> None:
        self.assertEqual("none", normalize_script_id("items.some_random_item"))

    def test_preserves_allowlisted_item_script(self) -> None:
        self.assertIn("items.door", PRESERVED_SCRIPT_IDS)
        self.assertEqual("items.door", normalize_script_id("items.door"))

    def test_defaults_to_none_for_empty_script_id(self) -> None:
        self.assertEqual("none", normalize_script_id(""))


class MigrateModernuoItemTemplatesTests(unittest.TestCase):
    def test_moves_files_to_root_and_rewrites_script_ids(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            temp_path = Path(tmp_dir)
            source_root = temp_path / ROOT_ITEMS_DIRECTORY / MODERNUO_ITEMS_ROOT.name
            target_root = temp_path / ROOT_ITEMS_DIRECTORY
            source_root.mkdir(parents=True)

            source_file = source_root / "construction.json"
            source_file.write_text(
                json.dumps(
                    [
                        {
                            "id": "bamboo_chair",
                            "scriptId": "items.bamboo_chair",
                        },
                        {
                            "id": "door",
                            "scriptId": "items.door",
                        },
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            migrated_files = migrate_modernuo_item_templates(source_root, target_root)

            self.assertEqual([target_root / "construction.json"], migrated_files)
            self.assertFalse(source_file.exists())
            self.assertFalse(source_root.exists())

            migrated_data = json.loads((target_root / "construction.json").read_text(encoding="utf-8"))
            self.assertEqual("none", migrated_data[0]["scriptId"])
            self.assertEqual("items.door", migrated_data[1]["scriptId"])

    def test_rejects_existing_target_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            temp_path = Path(tmp_dir)
            source_root = temp_path / ROOT_ITEMS_DIRECTORY / MODERNUO_ITEMS_ROOT.name
            target_root = temp_path / ROOT_ITEMS_DIRECTORY
            source_root.mkdir(parents=True)
            target_root.mkdir(parents=True, exist_ok=True)

            (source_root / "construction.json").write_text("[]\n", encoding="utf-8")
            (target_root / "construction.json").write_text("[]\n", encoding="utf-8")

            with self.assertRaisesRegex(FileExistsError, "construction.json"):
                migrate_modernuo_item_templates(source_root, target_root)


class ScriptCliImportsTests(unittest.TestCase):
    def test_migration_script_runs_as_direct_cli(self) -> None:
        result = subprocess.run(
            [sys.executable, "scripts/migrate_modernuo_item_templates.py", "--help"],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, result.returncode, result.stderr)

    def test_generator_script_runs_as_direct_cli(self) -> None:
        result = subprocess.run(
            [sys.executable, "scripts/generate_modernuo_static_templates.py", "--help"],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
