import json
import tempfile
import textwrap
import unittest
from pathlib import Path

from scripts.modernuo_converter.mapper import (
    load_item_template_catalog,
    map_pack_items_to_loot,
    map_to_template,
)
from scripts.modernuo_converter.parser import parse_directory, parse_file
from scripts.modernuo_converter.generators.loot_generator import generate_all_loot


class LoadItemTemplateCatalogTests(unittest.TestCase):
    def test_reads_template_layers_from_json_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            items_root = Path(tmp_dir)
            (items_root / "wearables.json").write_text(
                json.dumps(
                    [
                        {"id": "shirt", "layer": "Shirt"},
                        {"id": "boots", "layer": "Shoes"},
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            catalog = load_item_template_catalog(items_root)

            self.assertEqual("Shirt", catalog["shirt"]["layer"])
            self.assertEqual("Shoes", catalog["boots"]["layer"])


class MapToTemplateTests(unittest.TestCase):
    def test_maps_simple_mobile_to_default_variant(self) -> None:
        parsed = {
            "class_name": "Zombie",
            "category": "monsters",
            "body": 0x0003,
            "str_min": 40,
            "str_max": 40,
        }

        template = map_to_template(parsed, item_catalog={})

        self.assertNotIn("body", template)
        self.assertNotIn("skinHue", template)
        self.assertNotIn("hairHue", template)
        self.assertNotIn("fixedEquipment", template)
        self.assertNotIn("randomEquipment", template)
        self.assertEqual(1, len(template["variants"]))
        self.assertEqual("0x0003", template["variants"][0]["appearance"]["body"])
        self.assertEqual([], template["variants"][0]["equipment"])

    def test_expands_root_body_options_into_multiple_variants(self) -> None:
        parsed = {
            "class_name": "GreyWolf",
            "category": "animals",
            "body_options": [25, 27],
        }

        template = map_to_template(parsed, item_catalog={})

        self.assertEqual(2, len(template["variants"]))
        bodies = {variant["appearance"]["body"] for variant in template["variants"]}
        self.assertEqual({"0x0019", "0x001B"}, bodies)

    def test_maps_skin_hue_and_item_hue_helpers_to_variant_appearance_and_equipment(self) -> None:
        parsed = {
            "class_name": "Artist",
            "category": "town_npcs",
            "skin_hue": "Race.Human.RandomSkinHue()",
            "shared_equipment_groups": [
                [
                    {
                        "class_name": "Boots",
                        "hue": "Utility.RandomNeutralHue()",
                        "weight": 1,
                    }
                ]
            ],
        }
        catalog = {"boots": {"layer": "Shoes"}}

        template = map_to_template(parsed, item_catalog=catalog)
        variant = template["variants"][0]

        self.assertEqual("hue(1002:1058)", variant["appearance"]["skinHue"])
        self.assertEqual("hue(1801:1908)", variant["equipment"][0]["hue"])

    def test_maps_fixed_appearance_and_item_hues(self) -> None:
        parsed = {
            "class_name": "Artist",
            "category": "town_npcs",
            "body": 0x0190,
            "skin_hue": "33770",
            "hair_hue": "1150",
            "hair_style": 0x203B,
            "shared_equipment_groups": [
                [
                    {
                        "class_name": "Boots",
                        "hue": "0x047E",
                        "weight": 1,
                    }
                ]
            ],
        }
        catalog = {"boots": {"layer": "Shoes"}}

        template = map_to_template(parsed, item_catalog=catalog)
        variant = template["variants"][0]

        self.assertEqual("0x0190", variant["appearance"]["body"])
        self.assertEqual("0x83EA", variant["appearance"]["skinHue"])
        self.assertEqual("0x047E", variant["appearance"]["hairHue"])
        self.assertEqual(0x203B, variant["appearance"]["hairStyle"])
        self.assertEqual("0x047E", variant["equipment"][0]["hue"])

    def test_preserves_moongate_brain_overrides_for_special_townfolk(self) -> None:
        parsed = {
            "class_name": "Banker",
            "category": "town_npcs",
            "fight_mode": "None",
            "range_perception": 2,
            "range_fight": 1,
        }

        template = map_to_template(parsed, item_catalog={})

        self.assertEqual("banker_npc", template["id"])
        self.assertNotIn("brain", template)
        self.assertEqual(
            {
                "brain": "town_banker",
                "fightMode": "none",
                "rangePerception": 2,
                "rangeFight": 1,
            },
            template["ai"],
        )

    def test_maps_canonical_ai_metadata_without_root_brain(self) -> None:
        parsed = {
            "class_name": "JukaLord",
            "category": "monsters",
            "ai_type": "AI_Archer",
            "fight_mode": "Closest",
            "range_perception": 10,
            "range_fight": 3,
        }

        template = map_to_template(parsed, item_catalog={})

        self.assertNotIn("brain", template)
        self.assertEqual(
            {
                "brain": "ai_archer",
                "fightMode": "closest",
                "rangePerception": 10,
                "rangeFight": 3,
            },
            template["ai"],
        )

    def test_skips_unsupported_item_hair_and_facial_hues(self) -> None:
        parsed = {
            "class_name": "Artist",
            "category": "town_npcs",
            "skin_hue": "Race.Human.RandomSkinHue()",
            "hair_hue": "Race.Human.RandomHairHue()",
            "facial_hair_hue": "Race.Human.RandomHairHue()",
            "shared_equipment_groups": [
                [
                    {
                        "class_name": "Boots",
                        "hue": "Utility.RandomDyedHue()",
                        "weight": 1,
                    }
                ]
            ],
        }
        catalog = {"boots": {"layer": "Shoes"}}

        template = map_to_template(parsed, item_catalog=catalog)
        variant = template["variants"][0]

        self.assertNotIn("body", variant["appearance"])
        self.assertEqual("hue(1002:1058)", variant["appearance"]["skinHue"])
        self.assertNotIn("hairHue", variant["appearance"])
        self.assertNotIn("facialHairHue", variant["appearance"])
        self.assertNotIn("hue", variant["equipment"][0])

    def test_maps_gender_branch_shared_equipment_and_same_layer_weapon_choice_into_variants(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Brigand.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class Brigand : BaseCreature
                        {
                            [Constructible]
                            public Brigand() : base(AIType.AI_Melee)
                            {
                                Title = "the brigand";
                                Hue = Race.Human.RandomSkinHue();

                                if (Female = Utility.RandomBool())
                                {
                                    Body = 0x191;
                                    AddItem(new Skirt(Utility.RandomNeutralHue()));
                                }
                                else
                                {
                                    Body = 0x190;
                                    AddItem(new ShortPants(Utility.RandomNeutralHue()));
                                }

                                AddItem(new Boots(Utility.RandomNeutralHue()));
                                AddItem(new FancyShirt());
                                AddItem(new Bandana());

                                AddItem(
                                    Utility.Random(3) switch
                                    {
                                        0 => new Longsword(),
                                        1 => new Cutlass(),
                                        _ => new Broadsword()
                                    }
                                );
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "skirt": {"layer": "Pants"},
                "short_pants": {"layer": "Pants"},
                "boots": {"layer": "Shoes"},
                "fancy_shirt": {"layer": "Shirt"},
                "bandana": {"layer": "Helm"},
                "longsword": {"layer": "OneHanded"},
                "cutlass": {"layer": "OneHanded"},
                "broadsword": {"layer": "OneHanded"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual(2, len(template["variants"]))
            bodies = {variant["appearance"]["body"] for variant in template["variants"]}
            self.assertEqual({"0x0190", "0x0191"}, bodies)

            female_variants = [
                variant for variant in template["variants"] if variant["appearance"]["body"] == "0x0191"
            ]
            male_variants = [
                variant for variant in template["variants"] if variant["appearance"]["body"] == "0x0190"
            ]

            self.assertEqual(1, len(female_variants))
            self.assertEqual(1, len(male_variants))

            for variant in template["variants"]:
                layers = {entry["layer"] for entry in variant["equipment"]}
                self.assertIn("Pants", layers)
                self.assertIn("Shoes", layers)
                self.assertIn("Shirt", layers)
                self.assertIn("Helm", layers)
                self.assertIn("OneHanded", layers)

                weapon_entry = next(entry for entry in variant["equipment"] if entry["layer"] == "OneHanded")
                self.assertIn("items", weapon_entry)
                self.assertEqual(3, len(weapon_entry["items"]))

    def test_maps_two_step_female_random_bool_branch_into_variants(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "EscortableMage.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class EscortableMage : BaseCreature
                        {
                            [Constructible]
                            public EscortableMage() : base(AIType.AI_Mage)
                            {
                                Female = Utility.RandomBool();

                                if (Female)
                                {
                                    Body = 0x191;
                                    AddItem(new Skirt());
                                }
                                else
                                {
                                    Body = 0x190;
                                    AddItem(new ShortPants());
                                }
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "skirt": {"layer": "Pants"},
                "short_pants": {"layer": "Pants"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual(2, len(template["variants"]))
            bodies = {variant["appearance"]["body"] for variant in template["variants"]}
            self.assertEqual({"0x0190", "0x0191"}, bodies)

    def test_maps_switch_items_with_nested_constructor_calls(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "ShoeSwitcher.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class ShoeSwitcher : BaseCreature
                        {
                            [Constructible]
                            public ShoeSwitcher() : base(AIType.AI_Melee)
                            {
                                Body = 0x190;
                                AddItem(
                                    Utility.Random(2) switch
                                    {
                                        0 => new Boots(Utility.RandomNeutralHue()),
                                        _ => new Shoes(Utility.RandomNeutralHue())
                                    }
                                );
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "boots": {"layer": "Shoes"},
                "shoes": {"layer": "Shoes"},
            }

            template = map_to_template(parsed, item_catalog=catalog)
            equipment = template["variants"][0]["equipment"]

            self.assertEqual(1, len(equipment))
            self.assertEqual("Shoes", equipment[0]["layer"])
            self.assertEqual(2, len(equipment[0]["items"]))
            self.assertEqual("hue(1801:1908)", equipment[0]["items"][0]["hue"])
            self.assertEqual("hue(1801:1908)", equipment[0]["items"][1]["hue"])

    def test_maps_object_initializer_hue_with_other_properties(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "InitializerShoes.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class InitializerShoes : BaseCreature
                        {
                            [Constructible]
                            public InitializerShoes() : base(AIType.AI_Melee)
                            {
                                Body = 0x190;
                                Hue = Race.Human.RandomSkinHue();
                                AddItem(new Boots { Hue = Utility.RandomNeutralHue(), Name = "boots" });
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {"boots": {"layer": "Shoes"}}

            template = map_to_template(parsed, item_catalog=catalog)
            appearance = template["variants"][0]["appearance"]
            equipment = template["variants"][0]["equipment"]

            self.assertNotIn("title", template)
            self.assertEqual("hue(1002:1058)", appearance["skinHue"])
            self.assertEqual(1, len(equipment))
            self.assertEqual("hue(1801:1908)", equipment[0]["hue"])

    def test_skips_switch_pool_when_any_arm_is_unsupported(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "PartialSwitcher.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class PartialSwitcher : BaseCreature
                        {
                            [Constructible]
                            public PartialSwitcher() : base(AIType.AI_Melee)
                            {
                                Body = 0x190;
                                AddItem(
                                    Utility.Random(3) switch
                                    {
                                        0 => new Boots(Utility.RandomNeutralHue()),
                                        1 => MakeShoes(),
                                        _ => new Shoes(Utility.RandomNeutralHue())
                                    }
                                );
                            }

                            private static Item MakeShoes()
                            {
                                return new Shoes();
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "boots": {"layer": "Shoes"},
                "shoes": {"layer": "Shoes"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual([], template["variants"][0]["equipment"])

    def test_maps_variant_specific_hair_hue_and_style(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "StyledEscort.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class StyledEscort : BaseCreature
                        {
                            [Constructible]
                            public StyledEscort() : base(AIType.AI_Mage)
                            {
                                if (Female = Utility.RandomBool())
                                {
                                    Body = 0x191;
                                    HairHue = 1150;
                                    HairItemID = 0x203B;
                                }
                                else
                                {
                                    Body = 0x190;
                                    HairHue = 1151;
                                    HairItemID = 0x203C;
                                }
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            template = map_to_template(parsed, item_catalog={})

            self.assertEqual(2, len(template["variants"]))
            appearance_by_body = {
                variant["appearance"]["body"]: variant["appearance"] for variant in template["variants"]
            }
            self.assertEqual("0x047E", appearance_by_body["0x0191"]["hairHue"])
            self.assertEqual(0x203B, appearance_by_body["0x0191"]["hairStyle"])
            self.assertEqual("0x047F", appearance_by_body["0x0190"]["hairHue"])
            self.assertEqual(0x203C, appearance_by_body["0x0190"]["hairStyle"])

    def test_expands_classic_switch_add_item_group_into_distinct_variants(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "SamuraiWeaponSwitcher.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class SamuraiWeaponSwitcher : BaseCreature
                        {
                            [Constructible]
                            public SamuraiWeaponSwitcher() : base(AIType.AI_Melee)
                            {
                                Body = 0x190;

                                switch (Utility.Random(3))
                                {
                                    case 0:
                                        {
                                            AddItem(new Lajatang());
                                            break;
                                        }
                                    case 1:
                                        {
                                            AddItem(new Wakizashi());
                                            break;
                                        }
                                    case 2:
                                        {
                                            AddItem(new NoDachi());
                                            break;
                                        }
                                }

                                AddItem(new Boots());
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "lajatang": {"layer": "TwoHanded"},
                "wakizashi": {"layer": "OneHanded"},
                "no_dachi": {"layer": "TwoHanded"},
                "boots": {"layer": "Shoes"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual(3, len(template["variants"]))
            equipment_layers = [
                {entry["layer"] for entry in variant["equipment"]}
                for variant in template["variants"]
            ]
            self.assertTrue(all("Shoes" in layers for layers in equipment_layers))
            self.assertEqual(
                {"OneHanded", "TwoHanded"},
                {next(layer for layer in layers if layer != "Shoes") for layers in equipment_layers},
            )

    def test_maps_base_vendor_init_outfit_and_override_into_variant_equipment(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "BaseVendor.cs").write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public abstract class BaseVendor : BaseCreature
                        {
                            public virtual void InitOutfit()
                            {
                                AddItem(
                                    Utility.Random(3) switch
                                    {
                                        0 => new FancyShirt(),
                                        1 => new Doublet(),
                                        _ => new Shirt()
                                    }
                                );

                                AddItem(Utility.RandomBool() ? new Shoes() : new Sandals());

                                if (Female)
                                {
                                    AddItem(Utility.RandomBool() ? new ShortPants() : new Kilt());
                                }
                                else
                                {
                                    AddItem(Utility.RandomBool() ? new LongPants() : new ShortPants());
                                }
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            source_path = tmp_path / "Alchemist.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class Alchemist : BaseVendor
                        {
                            [Constructible]
                            public Alchemist() : base("the alchemist")
                            {
                            }

                            public override void InitOutfit()
                            {
                                base.InitOutfit();
                                AddItem(new Robe());
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "fancy_shirt": {"layer": "Shirt"},
                "doublet": {"layer": "Shirt"},
                "shirt": {"layer": "Shirt"},
                "shoes": {"layer": "Shoes"},
                "sandals": {"layer": "Shoes"},
                "short_pants": {"layer": "Pants"},
                "kilt": {"layer": "Pants"},
                "long_pants": {"layer": "Pants"},
                "robe": {"layer": "OuterTorso"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual(2, len(template["variants"]))
            variants = {variant["name"]: variant for variant in template["variants"]}

            female_equipment = variants["female"]["equipment"]
            male_equipment = variants["male"]["equipment"]

            female_torso = next(entry for entry in female_equipment if entry["layer"] == "Shirt")
            female_shoes = next(entry for entry in female_equipment if entry["layer"] == "Shoes")
            female_pants = next(entry for entry in female_equipment if entry["layer"] == "Pants")
            female_robe = next(entry for entry in female_equipment if entry["layer"] == "OuterTorso")

            self.assertEqual(
                {"fancy_shirt", "doublet", "shirt"},
                {item["itemTemplateId"] for item in female_torso["items"]},
            )
            self.assertEqual(
                {"shoes", "sandals"},
                {item["itemTemplateId"] for item in female_shoes["items"]},
            )
            self.assertEqual(
                {"short_pants", "kilt"},
                {item["itemTemplateId"] for item in female_pants["items"]},
            )
            self.assertEqual("robe", female_robe["itemTemplateId"])

            male_pants = next(entry for entry in male_equipment if entry["layer"] == "Pants")
            male_robe = next(entry for entry in male_equipment if entry["layer"] == "OuterTorso")

            self.assertEqual(
                {"long_pants", "short_pants"},
                {item["itemTemplateId"] for item in male_pants["items"]},
            )
            self.assertEqual("robe", male_robe["itemTemplateId"])

    def test_maps_init_outfit_gender_branches_without_constructor_equipment(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "BaseEscortable.cs").write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public abstract class BaseEscortable : BaseCreature
                        {
                            public virtual void InitOutfit()
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            source_path = tmp_path / "Peasant.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class Peasant : BaseEscortable
                        {
                            [Constructible]
                            public Peasant() => Title = "the peasant";

                            public override void InitOutfit()
                            {
                                if (Female)
                                {
                                    AddItem(new PlainDress());
                                }
                                else
                                {
                                    AddItem(new Shirt());
                                }

                                AddItem(new ShortPants());

                                if (Female)
                                {
                                    AddItem(new Boots());
                                }
                                else
                                {
                                    AddItem(new Shoes());
                                }
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "plain_dress": {"layer": "OuterTorso"},
                "shirt": {"layer": "Shirt"},
                "short_pants": {"layer": "Pants"},
                "boots": {"layer": "Shoes"},
                "shoes": {"layer": "Shoes"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual(2, len(template["variants"]))
            variants = {variant["name"]: variant for variant in template["variants"]}

            female_layers = {entry["layer"] for entry in variants["female"]["equipment"]}
            male_layers = {entry["layer"] for entry in variants["male"]["equipment"]}

            self.assertEqual({"OuterTorso", "Pants", "Shoes"}, female_layers)
            self.assertEqual({"Shirt", "Pants", "Shoes"}, male_layers)

            female_torso = next(entry for entry in variants["female"]["equipment"] if entry["layer"] == "OuterTorso")
            male_torso = next(entry for entry in variants["male"]["equipment"] if entry["layer"] == "Shirt")

            self.assertEqual("plain_dress", female_torso["itemTemplateId"])
            self.assertEqual("shirt", male_torso["itemTemplateId"])

    def test_maps_wrapped_apply_hue_items_inside_init_outfit(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "BaseVendor.cs").write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public abstract class BaseVendor : BaseCreature
                        {
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            source_path = tmp_path / "HolyMage.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    using Server.Items;

                    namespace Server.Mobiles
                    {
                        public class HolyMage : BaseVendor
                        {
                            [Constructible]
                            public HolyMage() : base("the Holy Mage")
                            {
                            }

                            public Item ApplyHue(Item item, int hue)
                            {
                                item.Hue = hue;
                                return item;
                            }

                            public override void InitOutfit()
                            {
                                AddItem(ApplyHue(new Robe(), 0x47E));
                                AddItem(ApplyHue(new ThighBoots(), 0x47E));

                                if (Female)
                                {
                                    AddItem(ApplyHue(new LeatherGloves(), 0x47E));
                                }
                                else
                                {
                                    AddItem(ApplyHue(new PlateGloves(), 0x47E));
                                }

                                HairHue = 0x47E;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))
            self.assertIsNotNone(parsed)

            catalog = {
                "robe": {"layer": "OuterTorso"},
                "thigh_boots": {"layer": "Shoes"},
                "leather_gloves": {"layer": "Gloves"},
                "plate_gloves": {"layer": "Gloves"},
            }

            template = map_to_template(parsed, item_catalog=catalog)

            self.assertEqual(2, len(template["variants"]))
            variants = {variant["name"]: variant for variant in template["variants"]}

            for variant in variants.values():
                robe = next(entry for entry in variant["equipment"] if entry["layer"] == "OuterTorso")
                boots = next(entry for entry in variant["equipment"] if entry["layer"] == "Shoes")
                self.assertEqual("0x047E", robe["hue"])
                self.assertEqual("0x047E", boots["hue"])

            female_gloves = next(entry for entry in variants["female"]["equipment"] if entry["layer"] == "Gloves")
            male_gloves = next(entry for entry in variants["male"]["equipment"] if entry["layer"] == "Gloves")

            self.assertEqual("leather_gloves", female_gloves["itemTemplateId"])
            self.assertEqual("plate_gloves", male_gloves["itemTemplateId"])
            self.assertEqual("0x047E", variants["female"]["appearance"]["hairHue"])
            self.assertEqual("0x047E", variants["male"]["appearance"]["hairHue"])


class ParseFileTests(unittest.TestCase):
    def test_scopes_parse_file_to_the_selected_class_in_multi_class_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Bird.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Bird : BaseCreature
                        {
                            [Constructible]
                            public Bird() : base(AIType.AI_Animal)
                            {
                                Body = 6;
                            }

                            public override string DefaultName => "a bird";
                        }

                        public class TropicalBird : BaseCreature
                        {
                            [Constructible]
                            public TropicalBird() : base(AIType.AI_Animal)
                            {
                                Body = 7;
                            }

                            public override string DefaultName => "a tropical bird";
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual("Bird", parsed["class_name"])
            self.assertEqual("a bird", parsed["name"])
            self.assertEqual(6, parsed["body"])

    def test_parse_file_does_not_leak_default_name_from_later_class_in_same_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Bird.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Bird : BaseCreature
                        {
                            [Constructible]
                            public Bird() : base(AIType.AI_Animal)
                            {
                                Body = 6;
                            }
                        }

                        public class TropicalBird : BaseCreature
                        {
                            [Constructible]
                            public TropicalBird() : base(AIType.AI_Animal)
                            {
                                Body = 7;
                            }

                            public override string DefaultName => "a tropical bird";
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual("Bird", parsed["class_name"])
            self.assertNotIn("name", parsed)
            self.assertEqual(6, parsed["body"])

    def test_extracts_expression_bodied_constructor_and_inherits_parent_variants(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "Banker.cs").write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Banker : BaseVendor
                        {
                            [Constructible]
                            public Banker() : base("the banker")
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            child_path = tmp_path / "Minter.cs"
            child_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles;

                    public partial class Minter : Banker
                    {
                        [Constructible]
                        public Minter() => Title = "the minter";
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(child_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(2, len(parsed["variants"]))
            appearances = {
                variant["appearance"]["body"]: variant["appearance"]
                for variant in parsed["variants"]
            }
            self.assertEqual({400, 401}, set(appearances))
            self.assertEqual("Race.Human.RandomSkinHue()", appearances[400]["skin_hue"])
            self.assertEqual("Race.Human.RandomSkinHue()", appearances[401]["skin_hue"])

    def test_extracts_default_human_variants_for_base_guildmaster(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "BardGuildmaster.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class BardGuildmaster : BaseGuildmaster
                        {
                            [Constructible]
                            public BardGuildmaster() : base("bard")
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(2, len(parsed["variants"]))
            appearances = {
                variant["appearance"]["body"]: variant["appearance"]
                for variant in parsed["variants"]
            }
            self.assertEqual({400, 401}, set(appearances))
            self.assertEqual("Race.Human.RandomSkinHue()", appearances[400]["skin_hue"])
            self.assertEqual("Race.Human.RandomSkinHue()", appearances[401]["skin_hue"])

    def test_extracts_random_list_body_options(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "RandomBodyMage.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class RandomBodyMage : BaseCreature
                        {
                            [Constructible]
                            public RandomBodyMage() : base(AIType.AI_Mage)
                            {
                                Body = Utility.RandomList(0x190, 0x191);
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual([0x190, 0x191], parsed["body_options"])

    def test_extracts_body_options_from_ternary_body_assignment(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "WhiteWyrm.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class WhiteWyrm : BaseCreature
                        {
                            [Constructible]
                            public WhiteWyrm() : base(AIType.AI_Mage)
                            {
                                Body = Utility.RandomBool() ? 180 : 49;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual([180, 49], parsed["body_options"])

    def test_extracts_body_after_if_else_control_block(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Bird.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Bird : BaseCreature
                        {
                            [Constructible]
                            public Bird() : base(AIType.AI_Animal)
                            {
                                if (Utility.RandomBool())
                                {
                                    Hue = 0x901;
                                }
                                else
                                {
                                    Hue = 0x902;
                                }

                                Body = 6;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(6, parsed["body"])

    def test_extracts_last_body_assignment_from_if_else_branches(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "EvilMage.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class EvilMage : BaseCreature
                        {
                            [Constructible]
                            public EvilMage() : base(AIType.AI_Mage)
                            {
                                if (Core.UOR)
                                {
                                    Body = 124;
                                }
                                else
                                {
                                    Body = 0x190;
                                    Hue = Race.Human.RandomSkinHue();
                                }
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(0x190, parsed["body"])
            self.assertEqual("Race.Human.RandomSkinHue()", parsed["skin_hue"])

    def test_inherits_ai_metadata_from_invoked_parent_constructor_overload(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "BaseMob.cs").write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class BaseMob : BaseCreature
                        {
                            public BaseMob() : base(AIType.AI_Melee, FightMode.Closest, 10, 1)
                            {
                            }

                            public BaseMob(int perception) : base(
                                AIType.AI_Mage,
                                FightMode.Evil,
                                rangePerception: perception,
                                rangeFight: 4)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            child_path = tmp_path / "ChildMob.cs"
            child_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class ChildMob : BaseMob
                        {
                            [Constructible]
                            public ChildMob() : base(perception: 22)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(child_path))

            self.assertIsNotNone(parsed)
            self.assertEqual("AI_Mage", parsed["ai_type"])
            self.assertEqual("Evil", parsed["fight_mode"])
            self.assertEqual(22, parsed["range_perception"])
            self.assertEqual(4, parsed["range_fight"])

    def test_extracts_base_mount_body_and_ai_from_mixed_signature_constructor_arguments(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "DesertOstard.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class DesertOstard : BaseMount
                        {
                            [Constructible]
                            public DesertOstard() : base(0xD2, 0x3EA3, AIType.AI_Animal, FightMode.Aggressor, 17, 5)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(0xD2, parsed["body"])
            self.assertEqual("AI_Animal", parsed["ai_type"])
            self.assertEqual("Aggressor", parsed["fight_mode"])
            self.assertEqual(17, parsed["range_perception"])
            self.assertEqual(5, parsed["range_fight"])

    def test_extracts_ai_metadata_from_symbolic_range_arguments(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Archer.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Archer : BaseCreature
                        {
                            const int PerceptionRange = 12;
                            static readonly int FightRange = 4;

                            [Constructible]
                            public Archer() : base(AIType.AI_Archer, FightMode.Closest, PerceptionRange, FightRange)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual("AI_Archer", parsed["ai_type"])
            self.assertEqual("Closest", parsed["fight_mode"])
            self.assertEqual(12, parsed["range_perception"])
            self.assertEqual(4, parsed["range_fight"])

    def test_extracts_ai_metadata_from_base_constructor_arguments(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "JukaLord.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class JukaLord : BaseCreature
                        {
                            [Constructible]
                            public JukaLord() : base(AIType.AI_Archer, FightMode.Closest, 10, 3)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual("AI_Archer", parsed["ai_type"])
            self.assertEqual("Closest", parsed["fight_mode"])
            self.assertEqual(10, parsed["range_perception"])
            self.assertEqual(3, parsed["range_fight"])

    def test_inherits_ai_metadata_from_parent_constructor_without_constructible_attribute(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            base_vendor_path = tmp_path / "BaseVendor.cs"
            alchemist_path = tmp_path / "Alchemist.cs"

            base_vendor_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public abstract class BaseVendor : BaseCreature
                        {
                            public BaseVendor(string title = null) : base(AIType.AI_Vendor, FightMode.None, 2)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            alchemist_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Alchemist : BaseVendor
                        {
                            [Constructible]
                            public Alchemist() : base("the alchemist")
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(alchemist_path))

            self.assertIsNotNone(parsed)
            self.assertEqual("AI_Vendor", parsed["ai_type"])
            self.assertEqual("None", parsed["fight_mode"])
            self.assertEqual(2, parsed["range_perception"])
            self.assertEqual(1, parsed["range_fight"])

    def test_extracts_body_from_custom_mount_base_constructor_arguments(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "CoMWarHorse.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class CoMWarHorse : BaseWarHorse
                        {
                            [Constructible]
                            public CoMWarHorse() : base(0x77, 0x3EB1)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(0x77, parsed["body"])

    def test_extracts_body_from_named_parent_class(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "OrcishLord.cs").write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class OrcishLord : BaseCreature
                        {
                            [Constructible]
                            public OrcishLord() : base(AIType.AI_Melee)
                            {
                                Body = 138;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            child_path = tmp_path / "SpawnedOrcishLord.cs"
            child_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class SpawnedOrcishLord : OrcishLord
                        {
                            [Constructible]
                            public SpawnedOrcishLord()
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(child_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(138, parsed["body"])

    def test_extracts_body_with_comments_in_constructor_and_inherited_parent(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_path = Path(tmp_dir)
            (tmp_path / "Troglodyte.cs").write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Troglodyte : BaseCreature
                        {
                            [Constructible]
                            public Troglodyte() : base(AIType.AI_Melee) // comment after base
                            {
                                // parser used to split on this semicolon;
                                Body = 267;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            child_path = tmp_path / "Lurg.cs"
            child_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Lurg : Troglodyte
                        {
                            [Constructible]
                            public Lurg()
                            {
                                Hue = 0x455;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )
            leviathan_path = tmp_path / "Leviathan.cs"
            leviathan_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Leviathan : BaseCreature
                        {
                            [Constructible]
                            public Leviathan() : base(AIType.AI_Mage)
                            {
                                // copied from krakens;
                                Body = 77;
                                Hue = 0x481;
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            inherited = parse_file(str(child_path))
            direct = parse_file(str(leviathan_path))

            self.assertIsNotNone(inherited)
            self.assertEqual(267, inherited["body"])
            self.assertEqual("0x455", inherited["skin_hue"])

            self.assertIsNotNone(direct)
            self.assertEqual(77, direct["body"])
            self.assertEqual("0x481", direct["skin_hue"])

    def test_extracts_default_human_variants_for_base_vendor(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Banker.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Banker : BaseVendor
                        {
                            [Constructible]
                            public Banker() : base("the banker")
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual(2, len(parsed["variants"]))
            appearances = {
                variant["appearance"]["body"]: variant["appearance"]
                for variant in parsed["variants"]
            }
            self.assertEqual({0x190, 0x191}, set(appearances))
            self.assertEqual("Race.Human.RandomSkinHue()", appearances[0x190]["skin_hue"])
            self.assertEqual("Race.Human.RandomSkinHue()", appearances[0x191]["skin_hue"])

    def test_skips_item_derived_constructibles(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "EtherealMount.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class EtherealMount : Item
                        {
                            [Constructible]
                            public EtherealMount(int itemID, int mountID) : base(itemID)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNone(parsed)

    def test_ignores_commented_loot_pack_references(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "UnfrozenMummy.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class UnfrozenMummy : BaseCreature
                        {
                            [Constructible]
                            public UnfrozenMummy() : base(AIType.AI_Mage)
                            {
                            }

                            public override void GenerateLoot()
                            {
                                AddLoot(LootPack.UltraRich, 2);
                                // AddLoot( LootPack.Parrot );
                                /*
                                AddLoot(LootPack.Miscellaneous);
                                */
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_file(str(source_path))

            self.assertIsNotNone(parsed)
            self.assertEqual([{"pack": "UltraRich", "count": 2}], parsed["loot"])


class MapPackItemsToLootTests(unittest.TestCase):
    def test_maps_special_pack_items_to_supported_item_references(self) -> None:
        parsed = {
            "class_name": "StoneGargoyle",
            "pack_items": [
                {"item": "Reagent", "amount": 3},
                {"item": "RandomTalisman", "amount": 1},
                {"item": "Seed", "amount": 1},
                {"item": "MetalChest", "amount": 1},
                {"item": "GargoylesPickaxe", "amount": 1},
            ],
        }

        loot = map_pack_items_to_loot(parsed)

        self.assertIsNotNone(loot)
        self.assertEqual(
            [
                {"itemTag": "reagents", "chance": 1.0, "amount": 3},
                {"itemTag": "talismans", "chance": 1.0, "amount": 1},
                {"itemTemplateId": "seed", "chance": 1.0, "amount": 1},
                {"itemTemplateId": "metal_chest", "chance": 1.0, "amount": 1},
                {"itemTemplateId": "gargoyles_pickaxe", "chance": 1.0, "amount": 1},
            ],
            loot["entries"],
        )


class ParseDirectoryTests(unittest.TestCase):
    def test_parse_directory_emits_each_mobile_class_from_multi_class_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Bird.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class Bird : BaseCreature
                        {
                            [Constructible]
                            public Bird() : base(AIType.AI_Animal)
                            {
                                Body = 6;
                            }

                            public override string DefaultName => "a bird";
                        }

                        public class TropicalBird : BaseCreature
                        {
                            [Constructible]
                            public TropicalBird() : base(AIType.AI_Animal)
                            {
                                Body = 7;
                            }

                            public override string DefaultName => "a tropical bird";
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_directory(tmp_dir, "animals", ".")

            self.assertEqual(2, len(parsed))
            parsed_by_class = {entry["class_name"]: entry for entry in parsed}
            self.assertEqual("a bird", parsed_by_class["Bird"]["name"])
            self.assertEqual(6, parsed_by_class["Bird"]["body"])
            self.assertEqual("a tropical bird", parsed_by_class["TropicalBird"]["name"])
            self.assertEqual(7, parsed_by_class["TropicalBird"]["body"])

    def test_parse_directory_skips_item_derived_classes_from_multi_class_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            source_path = Path(tmp_dir) / "Ethereals.cs"
            source_path.write_text(
                textwrap.dedent(
                    """
                    namespace Server.Mobiles
                    {
                        public class EtherealMount : Item
                        {
                            [Constructible]
                            public EtherealMount(int itemID, int mountID) : base(itemID)
                            {
                            }
                        }

                        public class EtherealHorse : EtherealMount
                        {
                            [Constructible]
                            public EtherealHorse() : base(0x20DD, 0x3EAA)
                            {
                            }
                        }
                    }
                    """
                ),
                encoding="utf-8",
            )

            parsed = parse_directory(tmp_dir, "animals", ".")

            self.assertEqual([], parsed)


class LootGeneratorTests(unittest.TestCase):
    def test_generate_all_loot_removes_stale_creature_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            output_dir = Path(tmp_dir)
            stale_path = output_dir / "templates" / "loot" / "creatures" / "creature_malefic.json"
            stale_path.parent.mkdir(parents=True, exist_ok=True)
            stale_path.write_text("[]\n", encoding="utf-8")

            generated_paths = generate_all_loot(
                [
                    {
                        "type": "loot",
                        "id": "creature.bone_magi",
                        "name": "BoneMagi",
                        "category": "loot",
                        "description": "",
                        "mode": "additive",
                        "entries": [{"itemTemplateId": "bone", "chance": 1.0, "amount": 1}],
                    }
                ],
                str(output_dir),
            )

            self.assertEqual(
                [str(output_dir / "templates" / "loot" / "creatures" / "creature_bone_magi.json")],
                generated_paths,
            )
            self.assertFalse(stale_path.exists())


if __name__ == "__main__":
    unittest.main()
