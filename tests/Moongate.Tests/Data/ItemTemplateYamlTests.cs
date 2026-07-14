using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Types;
using SquidStd.Core.Yaml;

namespace Moongate.Tests.Data;

public class ItemTemplateYamlTests
{
    private const string Sample =
        "- Id: broadsword\n" +
        "  Name: Broadsword\n" +
        "  Category: Weapons\n" +
        "  Description: A sword.\n" +
        "  ItemId: 3934\n" +
        "  Hue: 0\n" +
        "  GoldValue: 35\n" +
        "  Weight: 6.0\n" +
        "  ScriptId: none\n" +
        "  IsMovable: true\n" +
        "  Rarity: Common\n" +
        "  Tags:\n" +
        "  - modernuo\n" +
        "  Equip:\n" +
        "    Layer: OneHanded\n" +
        "    HitPoints: 50\n" +
        "    StrengthReq: 25\n" +
        "  Weapon:\n" +
        "    LowDamage: 5\n" +
        "    HighDamage: 7\n" +
        "    Speed: 30\n" +
        "    WeaponSkill: Swords\n" +
        "- Id: gm_body_bag\n" +
        "  Name: Body Bag\n" +
        "  Category: Special\n" +
        "  Description: GM bag.\n" +
        "  ItemId: 3703\n" +
        "  Hue: 0\n" +
        "  GoldValue: 0\n" +
        "  Weight: 1.0\n" +
        "  ScriptId: none\n" +
        "  IsMovable: true\n" +
        "  Rarity: Artifact\n" +
        "  Visibility: GameMaster\n" +
        "  Tags: []\n" +
        "  Container:\n" +
        "    MaxItems: 125\n" +
        "    GumpId: 60\n" +
        "    Contents:\n" +
        "    - gm_robe\n";

    [Fact]
    public void Deserialize_BindsBaseSpecsAndReusedLayerEnum()
    {
        var path = Path.Combine(Path.GetTempPath(), "it-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, Sample);

        try
        {
            var items = YamlUtils.DeserializeFromFile<ItemTemplate[]>(path)!;

            Assert.Equal(2, items.Length);

            var sword = items[0];
            Assert.Equal("broadsword", sword.Id);
            Assert.Equal(ItemRarityType.Common, sword.Rarity);
            Assert.Equal(LayerType.OneHanded, sword.Equip!.Layer);
            Assert.Equal(25, sword.Equip.StrengthReq);
            Assert.Equal("Swords", sword.Weapon!.WeaponSkill);
            Assert.Null(sword.Container);

            var bag = items[1];
            Assert.Equal(ItemRarityType.Artifact, bag.Rarity);
            Assert.Equal("GameMaster", bag.Visibility);
            Assert.Equal(125, bag.Container!.MaxItems);
            Assert.Equal("gm_robe", bag.Container.Contents!.Single());
            Assert.Null(bag.Equip);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
