import json
import tempfile
import unittest
from pathlib import Path

from scripts.modernuo_loot_tooling import (
    DiceRange,
    build_loot_id,
    build_output_paths,
    extract_balanced_block,
    load_item_template_index,
    normalize_identifier,
    parse_dice_range,
    resolve_item_reference,
    split_top_level_arguments,
)


class NormalizeIdentifierTests(unittest.TestCase):
    def test_converts_pascal_case_to_snake_case(self) -> None:
        self.assertEqual("giant_spider", normalize_identifier("GiantSpider"))


class BuildLootIdTests(unittest.TestCase):
    def test_prefixes_without_modernuo_branding(self) -> None:
        self.assertEqual("creature.zombie", build_loot_id("Zombie", "creature"))


class BuildOutputPathsTests(unittest.TestCase):
    def test_returns_runtime_loot_paths(self) -> None:
        paths = build_output_paths(Path("/repo"))

        self.assertEqual(Path("/repo/moongate_data/templates/loot/creatures.json"), paths["creatures"])
        self.assertEqual(Path("/repo/moongate_data/templates/loot/treasure_chests.json"), paths["treasure_chests"])
        self.assertEqual(
            Path("/repo/moongate_data/templates/loot/fillable_containers.json"),
            paths["fillable_containers"],
        )


class ParseDiceRangeTests(unittest.TestCase):
    def test_parses_integer_quantity(self) -> None:
        self.assertEqual(DiceRange(4, 4), parse_dice_range(4))

    def test_parses_dice_expression_bounds(self) -> None:
        self.assertEqual(DiceRange(454, 650), parse_dice_range('"4d50+450"'))


class SplitTopLevelArgumentsTests(unittest.TestCase):
    def test_splits_nested_arguments_without_breaking_inner_lists(self) -> None:
        arguments = 'false, AosMagicItemsRichType1, 100.00, "4d50+450", 1, 5, 0, 75'

        self.assertEqual(
            ["false", "AosMagicItemsRichType1", "100.00", '"4d50+450"', "1", "5", "0", "75"],
            split_top_level_arguments(arguments),
        )


class ExtractBalancedBlockTests(unittest.TestCase):
    def test_extracts_parenthesized_content(self) -> None:
        content, end = extract_balanced_block("abc(def(ghi))j", 3, "(", ")")

        self.assertEqual("def(ghi)", content)
        self.assertEqual(13, end)


class ItemResolutionTests(unittest.TestCase):
    def test_resolve_item_reference_prefers_exact_item_template(self) -> None:
        item_index = {"gold": {"id": "gold"}}
        tag_index = {"gems": ["amber"]}

        self.assertEqual({"itemTemplateId": "gold"}, resolve_item_reference("Gold", item_index, tag_index))

    def test_resolve_item_reference_maps_polymorphic_type_to_existing_tag(self) -> None:
        item_index = {"bow": {"id": "bow"}}
        tag_index = {"weapons": ["bow"]}

        self.assertEqual({"itemTag": "weapons"}, resolve_item_reference("BaseWeapon", item_index, tag_index))

    def test_resolve_item_reference_maps_known_alias_to_existing_template(self) -> None:
        item_index = {"night_sight_potion": {"id": "night_sight_potion"}}
        tag_index = {}

        self.assertEqual(
            {"itemTemplateId": "night_sight_potion"},
            resolve_item_reference("NightSightPotion", item_index, tag_index),
        )


class LoadItemTemplateIndexTests(unittest.TestCase):
    def test_loads_items_and_tags_from_repo_layout(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            repo_root = Path(tmp_dir)
            items_root = repo_root / "moongate_data/templates/items"
            items_root.mkdir(parents=True)
            (items_root / "misc.json").write_text(
                json.dumps(
                    [
                        {"id": "gold", "tags": ["currency"]},
                        {"id": "amber", "tags": ["gems"]},
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            item_index, tag_index = load_item_template_index(repo_root)

            self.assertIn("gold", item_index)
            self.assertEqual(["amber"], tag_index["gems"])


if __name__ == "__main__":
    unittest.main()
