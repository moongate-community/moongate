import json
import tempfile
import unittest
from pathlib import Path

from scripts.generate_loot_templates import build_argument_parser, generate_from_paths


def write_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


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


if __name__ == "__main__":
    unittest.main()
