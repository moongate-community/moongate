import json
import tempfile
import unittest
from pathlib import Path

from scripts.generate_modernuo_static_templates import (
    generate_from_audit_file,
    group_templates_by_category,
    map_audit_item_to_template,
    merge_templates_into_category_file,
    should_include_item,
)


class ShouldIncludeItemTests(unittest.TestCase):
    def test_includes_static_candidate_from_allowed_family(self) -> None:
        item = {
            "sourceFile": "Construction/Chairs/Chairs.cs",
            "staticDecorativeCandidate": True,
            "itemIds": ["0x0B5B"],
        }

        self.assertTrue(should_include_item("construction", item))

    def test_excludes_books_even_if_marked_static(self) -> None:
        item = {
            "sourceFile": "Books/Defined/LibraryBooks.cs",
            "staticDecorativeCandidate": True,
            "itemIds": ["0x0002"],
        }

        self.assertFalse(should_include_item("books", item))


class MapAuditItemToTemplateTests(unittest.TestCase):
    def test_maps_static_audit_record_to_moongate_template(self) -> None:
        item = {
            "className": "BambooChair",
            "defaultName": "Bamboo Chair",
            "itemIds": ["0x0B5B"],
            "flipIds": ["0x0B5B", "0x0B5C"],
            "weight": 20,
            "movable": True,
        }

        template = map_audit_item_to_template("Construction", "construction", item)

        self.assertEqual("item", template["type"])
        self.assertEqual("bamboo_chair", template["id"])
        self.assertEqual("Bamboo Chair", template["name"])
        self.assertEqual("Construction", template["category"])
        self.assertEqual("0x0B5B", template["itemId"])
        self.assertIn("modernuo", template["tags"])
        self.assertIn("construction", template["tags"])
        self.assertIn("flippable", template["tags"])


class MergeTemplatesIntoCategoryFileTests(unittest.TestCase):
    def test_appends_only_missing_templates(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            category_file = Path(tmp_dir) / "construction.json"
            category_file.write_text(
                json.dumps(
                    [
                        {"id": "bamboo_chair", "name": "Existing"},
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            merge_templates_into_category_file(
                category_file,
                [
                    {"id": "bamboo_chair", "name": "New"},
                    {"id": "barrel_lid", "name": "Barrel Lid"},
                ],
            )

            data = json.loads(category_file.read_text(encoding="utf-8"))
            self.assertEqual(["bamboo_chair", "barrel_lid"], [item["id"] for item in data])
            self.assertEqual("Existing", data[0]["name"])

    def test_creates_new_file_when_category_file_does_not_exist(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            category_file = Path(tmp_dir) / "construction.json"

            merge_templates_into_category_file(
                category_file,
                [
                    {"id": "barrel_lid", "name": "Barrel Lid"},
                    {"id": "bamboo_chair", "name": "Bamboo Chair"},
                ],
            )

            data = json.loads(category_file.read_text(encoding="utf-8"))
            self.assertEqual(["bamboo_chair", "barrel_lid"], [item["id"] for item in data])


class GroupTemplatesByCategoryTests(unittest.TestCase):
    def test_groups_templates_by_category_id(self) -> None:
        grouped = group_templates_by_category(
            [
                ("construction", {"id": "bamboo_chair"}),
                ("plants_flowers", {"id": "potted_plant"}),
            ]
        )

        self.assertEqual(["construction", "plants_flowers"], sorted(grouped.keys()))


class GenerateFromAuditFileTests(unittest.TestCase):
    def test_generates_only_allowed_static_templates(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            temp_path = Path(tmp_dir)
            audit_file = temp_path / "audit.json"
            output_root = temp_path / "items"
            audit_file.write_text(
                json.dumps(
                    {
                        "families": [
                            {
                                "id": "construction",
                                "sourcePath": "Construction",
                                "items": [
                                    {
                                        "className": "BambooChair",
                                        "defaultName": "Bamboo Chair",
                                        "itemIds": ["0x0B5B"],
                                        "flipIds": [],
                                        "weight": 20,
                                        "movable": True,
                                        "staticDecorativeCandidate": True,
                                    }
                                ],
                            },
                            {
                                "id": "books",
                                "sourcePath": "Books",
                                "items": [
                                    {
                                        "className": "GrammarOfOrcish",
                                        "defaultName": "A Grammar of Orcish",
                                        "itemIds": ["0x0002"],
                                        "flipIds": [],
                                        "weight": 1,
                                        "movable": True,
                                        "staticDecorativeCandidate": True,
                                    }
                                ],
                            },
                        ]
                    },
                    indent=2,
                ),
                encoding="utf-8",
            )

            generate_from_audit_file(audit_file, output_root)

            construction_file = output_root / "construction.json"
            books_file = output_root / "books.json"

            self.assertTrue(construction_file.exists())
            self.assertFalse(books_file.exists())

            construction = json.loads(construction_file.read_text(encoding="utf-8"))
            self.assertEqual(["bamboo_chair"], [item["id"] for item in construction])


if __name__ == "__main__":
    unittest.main()
