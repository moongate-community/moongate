import json
import tempfile
import unittest
from pathlib import Path

from scripts.generate_loot_templates import build_argument_parser, generate_from_paths


def write_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def find_template(templates: list[dict], template_id: str) -> dict:
    for template in templates:
        if template.get("id") == template_id:
            return template

    raise AssertionError(f"Template '{template_id}' not found")


class GenerateLootTemplatesTests(unittest.TestCase):
    def test_generates_creature_fillable_and_treasure_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            repo_root = root / "repo"
            modernuo_root = root / "modernuo"

            write_file(
                repo_root / "moongate_data/templates/items/misc.json",
                json.dumps(
                    [
                        {"id": "gold", "tags": ["currency"]},
                        {"id": "bandage", "tags": ["misc"]},
                        {"id": "treasure_map", "tags": ["maps"]},
                    ],
                    indent=2,
                )
                + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/weapons.json",
                json.dumps([{"id": "bow", "tags": ["weapons"]}], indent=2) + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/gems.json",
                json.dumps([{"id": "amber", "tags": ["gems"]}], indent=2) + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/resources.json",
                json.dumps([{"id": "black_pearl", "tags": ["resources"]}], indent=2) + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/skill_items.json",
                json.dumps([{"id": "clumsy_scroll", "tags": ["skill items"]}], indent=2) + "\n",
            )

            write_file(
                modernuo_root / "Projects/UOContent/Misc/LootPack.cs",
                """
                public class LootPack
                {
                    public static readonly LootPackItem[] Gold = [new LootPackItem(typeof(Gold), 1)];
                    public static readonly LootPack Meager = new(
                        [new LootPackEntry(false, Gold, 100.00, 1)]
                    );
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Misc/Loot.cs",
                """
                public static class Loot
                {
                    public static Type[] WeaponTypes { get; } =
                    {
                        typeof(Bow), typeof(Bow)
                    };
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Mobiles/Monsters/Zombie.cs",
                """
                namespace Server.Mobiles
                {
                    public partial class Zombie : BaseCreature
                    {
                        public override void GenerateLoot()
                        {
                            AddLoot(LootPack.Meager);
                            PackGold(20, 40);
                            PackItem(new Bandage(2));
                        }
                    }
                }
                """,
            )
            write_file(
                modernuo_root
                / "Projects/UOContent/Items/Containers/Fillable Containers/FillableContent.ContentTypes.cs",
                """
                public partial class FillableContent
                {
                    private static readonly FillableContent Provisioner = new(
                        1,
                        [typeof(Provisioner)],
                        [
                            new FillableEntry(2, typeof(Bandage)),
                            new FillableEntry(1, Loot.WeaponTypes)
                        ]
                    );
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Items/Containers/TreasureMapChest.cs",
                """
                public partial class TreasureMapChest
                {
                    public static void Fill(LockableContainer cont, int level)
                    {
                        if (level == 0)
                        {
                            cont.DropItem(new Gold(Utility.RandomMinMax(50, 100)));
                            if (Utility.RandomDouble() < 0.75)
                            {
                                cont.DropItem(new TreasureMap(0, Map.Trammel));
                            }
                        }
                    }
                }
                """,
            )

            report = generate_from_paths(modernuo_root, repo_root)

            creatures = json.loads((repo_root / "moongate_data/templates/loot/creatures.json").read_text())
            fillable = json.loads(
                (repo_root / "moongate_data/templates/loot/fillable_containers.json").read_text()
            )
            treasure = json.loads((repo_root / "moongate_data/templates/loot/treasure_chests.json").read_text())

            self.assertEqual("creature.zombie", creatures[0]["id"])
            self.assertEqual("additive", creatures[0]["mode"])
            self.assertEqual("fillable.provisioner", fillable[0]["id"])
            self.assertEqual("weighted", fillable[0]["mode"])
            self.assertEqual("treasure_map.level_0", treasure[0]["id"])
            self.assertIn("creature.zombie", report["generated"])
            self.assertIn("fillable.provisioner", report["generated"])

    def test_default_repo_root_argument_is_current_directory(self) -> None:
        parser = build_argument_parser()

        args = parser.parse_args(["/tmp/modernuo"])

        self.assertEqual(Path("/tmp/modernuo"), args.modernuo_root)
        self.assertEqual(Path("."), args.repo_root)

    def test_generates_fillable_beverage_entries_from_known_pairs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            repo_root = root / "repo"
            modernuo_root = root / "modernuo"

            write_file(
                repo_root / "moongate_data/templates/items/food.json",
                json.dumps(
                    [
                        {"id": "pitcher_milk", "tags": ["beverage"]},
                        {"id": "bottle_ale", "tags": ["beverage"]},
                    ],
                    indent=2,
                )
                + "\n",
            )
            write_file(
                modernuo_root / "Projects/UOContent/Misc/LootPack.cs",
                """
                public class LootPack
                {
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Misc/Loot.cs",
                """
                public static class Loot
                {
                }
                """,
            )
            write_file(
                modernuo_root
                / "Projects/UOContent/Items/Containers/Fillable Containers/FillableContent.ContentTypes.cs",
                """
                public partial class FillableContent
                {
                    private static readonly FillableContent Tavern = new(
                        1,
                        [typeof(TavernKeeper)],
                        [
                            new FillableBvrge(1, typeof(BeverageBottle), BeverageType.Ale),
                            new FillableBvrge(1, typeof(Pitcher), BeverageType.Milk)
                        ]
                    );
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Items/Containers/TreasureMapChest.cs",
                """
                public partial class TreasureMapChest
                {
                    public static void Fill(LockableContainer cont, int level)
                    {
                    }
                }
                """,
            )

            report = generate_from_paths(modernuo_root, repo_root)

            fillable = json.loads(
                (repo_root / "moongate_data/templates/loot/fillable_containers.json").read_text()
            )

            self.assertEqual("fillable.tavern", fillable[0]["id"])
            self.assertEqual(
                ["bottle_ale", "pitcher_milk"],
                [entry["itemTemplateId"] for entry in fillable[0]["entries"]],
            )
            self.assertNotIn("fillable.Tavern: BeverageBottle", "\n".join(report["missingItems"]))

    def test_generates_loot_pack_tables_with_modernuo_prefix_and_ignores_commented_parrot_pack(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            repo_root = root / "repo"
            modernuo_root = root / "modernuo"

            write_file(
                repo_root / "moongate_data/templates/items/misc.json",
                json.dumps([{"id": "gold", "tags": ["currency"]}], indent=2) + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/gems.json",
                json.dumps([{"id": "amber", "tags": ["gems"]}], indent=2) + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/potions.json",
                json.dumps(
                    [
                        {"id": "agility_potion", "tags": ["potions"]},
                        {"id": "strength_potion", "tags": ["potions"]},
                    ],
                    indent=2,
                )
                + "\n",
            )

            write_file(
                modernuo_root / "Projects/UOContent/Misc/LootPack.cs",
                """
                public class LootPack
                {
                    public static readonly LootPackItem[] Gold = [new LootPackItem(typeof(Gold), 1)];
                    public static readonly LootPackItem[] GemItems = [new LootPackItem(typeof(Amber), 1)];
                    public static readonly LootPackItem[] PotionItems =
                    [
                        new LootPackItem(typeof(AgilityPotion), 1),
                        new LootPackItem(typeof(StrengthPotion), 1)
                    ];

                    public static readonly LootPack Average = new(
                        [
                            new LootPackEntry(false, Gold, 100.00, "2d10"),
                            new LootPackEntry(false, GemItems, 50.00, 1)
                        ]
                    );

                    public static readonly LootPack MlRich = new(
                        [new LootPackEntry(false, Gold, 100.00, "4d50+450")]
                    );

                    public static readonly LootPack AosFilthyRich = new(
                        [new LootPackEntry(false, Gold, 100.00, "4d50+450")]
                    );

                    /*
                    public static readonly LootPackItem[] ParrotItem =
                    [
                        new LootPackItem(typeof(ParrotItem), 1)
                    ];

                    public static readonly LootPack Parrot = new(
                        [new LootPackEntry(false, ParrotItem, 10.00, 1)]
                    );
                    */
                }
                """,
            )
            write_file(modernuo_root / "Projects/UOContent/Misc/Loot.cs", "public static class Loot { }\n")
            write_file(
                modernuo_root / "Projects/UOContent/Items/Containers/Fillable Containers/FillableContent.ContentTypes.cs",
                """
                public partial class FillableContent
                {
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Items/Containers/TreasureMapChest.cs",
                """
                public partial class TreasureMapChest
                {
                    public static void Fill(LockableContainer cont, int level)
                    {
                    }
                }
                """,
            )

            report = generate_from_paths(modernuo_root, repo_root)

            loot_packs = json.loads((repo_root / "moongate_data/templates/loot/loot_packs.json").read_text())
            generated_ids = {template["id"] for template in loot_packs}

            self.assertIn("loot_pack.average", generated_ids)
            self.assertIn("loot_pack.ml_rich", generated_ids)
            self.assertIn("loot_pack.aos_filthy_rich", generated_ids)
            self.assertNotIn("loot_pack.parrot", generated_ids)
            self.assertNotIn("ParrotItem", "\n".join(report["missingItems"]))

    def test_generates_special_creature_loot_references_and_support_items(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            repo_root = root / "repo"
            modernuo_root = root / "modernuo"

            write_file(
                repo_root / "moongate_data/templates/items/resources.json",
                json.dumps(
                    [
                        {"id": "black_pearl", "tags": ["modernuo", "resources"]},
                        {"id": "bloodmoss", "tags": ["modernuo", "resources"]},
                        {"id": "garlic", "tags": ["modernuo", "resources"]},
                        {"id": "ginseng", "tags": ["modernuo", "resources"]},
                        {"id": "mandrake_root", "tags": ["modernuo", "resources"]},
                        {"id": "nightshade", "tags": ["modernuo", "resources"]},
                        {"id": "spiders_silk", "tags": ["modernuo", "resources"]},
                        {"id": "sulfurous_ash", "tags": ["modernuo", "resources"]},
                        {"id": "board", "tags": ["modernuo", "resources"]},
                        {"id": "log", "tags": ["modernuo", "resources"]},
                    ],
                    indent=2,
                )
                + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/special.json",
                json.dumps(
                    [
                        {"id": "bone", "tags": ["modernuo", "special"]},
                        {"id": "plague_beast_gland", "tags": ["modernuo", "special"]},
                    ],
                    indent=2,
                )
                + "\n",
            )
            write_file(
                repo_root / "moongate_data/templates/items/talismans.json",
                json.dumps(
                    [
                        {"id": "runed_switch", "tags": ["modernuo", "talismans"]},
                        {"id": "runed_prism", "tags": ["modernuo", "talismans"]},
                    ],
                    indent=2,
                )
                + "\n",
            )

            write_file(
                modernuo_root / "Projects/UOContent/Misc/LootPack.cs",
                """
                public class LootPack
                {
                }
                """,
            )
            write_file(modernuo_root / "Projects/UOContent/Misc/Loot.cs", "public static class Loot { }\n")
            write_file(
                modernuo_root / "Projects/UOContent/Items/Containers/Fillable Containers/FillableContent.ContentTypes.cs",
                """
                public partial class FillableContent
                {
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Items/Containers/TreasureMapChest.cs",
                """
                public partial class TreasureMapChest
                {
                    public static void Fill(LockableContainer cont, int level)
                    {
                    }
                }
                """,
            )

            write_file(
                modernuo_root / "Projects/UOContent/Mobiles/Monsters/Humanoid/Magic/BoneMagi.cs",
                """
                namespace Server.Mobiles
                {
                    public partial class BoneMagi : BaseCreature
                    {
                        public override void GenerateLoot()
                        {
                            PackItem(new Bone());
                            PackReg(3);
                        }
                    }
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Mobiles/Monsters/ML/Special/Ilhenir.cs",
                """
                namespace Server.Mobiles
                {
                    public partial class Ilhenir : BaseCreature
                    {
                        public override void GenerateLoot()
                        {
                            PackItem(new RandomTalisman());
                        }
                    }
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Mobiles/Monsters/Plant/Melee/BogThing.cs",
                """
                namespace Server.Mobiles
                {
                    public partial class BogThing : BaseCreature
                    {
                        public override void GenerateLoot()
                        {
                            PackItem(new Seed());
                            PackReg(3);
                        }
                    }
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Mobiles/Monsters/Humanoid/Melee/StoneGargoyle.cs",
                """
                namespace Server.Mobiles
                {
                    public partial class StoneGargoyle : BaseCreature
                    {
                        public override void GenerateLoot()
                        {
                            PackItem(new GargoylesPickaxe());
                        }
                    }
                }
                """,
            )
            write_file(
                modernuo_root / "Projects/UOContent/Mobiles/Monsters/Misc/Melee/PlagueBeast.cs",
                """
                namespace Server.Mobiles
                {
                    public partial class PlagueBeast : BaseCreature
                    {
                        public override void GenerateLoot()
                        {
                            PackItem(new PlagueBeastGland());
                            PackItem(new MetalChest());
                            // PackItem(new ParrotItem());
                        }
                    }
                }
                """,
            )

            report = generate_from_paths(modernuo_root, repo_root)

            creatures = json.loads((repo_root / "moongate_data/templates/loot/creatures.json").read_text())
            support_items = json.loads((repo_root / "moongate_data/templates/items/loot_support.json").read_text())

            bone_magi = find_template(creatures, "creature.bone_magi")
            ilhenir = find_template(creatures, "creature.ilhenir")
            bog_thing = find_template(creatures, "creature.bog_thing")
            stone_gargoyle = find_template(creatures, "creature.stone_gargoyle")
            plague_beast = find_template(creatures, "creature.plague_beast")

            bone_magi_reagent_entry = next(
                entry for entry in bone_magi["entries"] if entry.get("itemTag") == "reagents"
            )
            ilhenir_talisman_entry = next(
                entry for entry in ilhenir["entries"] if entry.get("itemTag") == "talismans"
            )
            bog_thing_seed_entry = next(
                entry for entry in bog_thing["entries"] if entry.get("itemTemplateId") == "seed"
            )
            stone_gargoyle_pickaxe_entry = next(
                entry for entry in stone_gargoyle["entries"] if entry.get("itemTemplateId") == "gargoyles_pickaxe"
            )
            plague_beast_chest_entry = next(
                entry for entry in plague_beast["entries"] if entry.get("itemTemplateId") == "metal_chest"
            )

            self.assertNotIn("itemTemplateId", bone_magi_reagent_entry)
            self.assertEqual(3, bone_magi_reagent_entry["amount"])

            self.assertNotIn("itemTemplateId", ilhenir_talisman_entry)

            self.assertEqual("seed", bog_thing_seed_entry["itemTemplateId"])
            self.assertEqual("gargoyles_pickaxe", stone_gargoyle_pickaxe_entry["itemTemplateId"])
            self.assertEqual("metal_chest", plague_beast_chest_entry["itemTemplateId"])
            self.assertNotIn("ParrotItem", "\n".join(report["missingItems"]))

            support_item_ids = {item["id"] for item in support_items}
            self.assertEqual({"gargoyles_pickaxe", "metal_chest", "seed"}, support_item_ids)


if __name__ == "__main__":
    unittest.main()
