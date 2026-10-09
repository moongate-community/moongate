using Lua;
using Lua.Standard;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class CraftModuleTests
{
    private readonly CraftService _crafts = new(
        new StubDataLoaderService()
            .With(
                new CraftDefinition
                {
                    Id = "carpentry", Name = "Carpentry", Skill = "carpentry", Sound = 0x023D,
                    Group =
                    [
                        new()
                        {
                            Name = "Musical items",
                            Recipe =
                            [
                                new()
                                {
                                    Name = "Lute", Item = "0x0eb3_lute", SkillMin = 68.4, SkillMax = 93.4,
                                    Resources = [new() { Resource = "wood", Amount = 25 }, new() { Resource = "cloth", Amount = 10 }],
                                    Skills = [new() { Skill = "musicianship", Min = 45, Max = 70 }]
                                }
                            ]
                        }
                    ]
                }
            )
            .With(new CraftResourceList { Id = "wood", Templates = ["0x1bd7_board", "0x1bda_board"] })
    );

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(new ItemTemplate { Id = "0x0eb3_lute", ItemId = new Serial(0x0EB3) })
    );

    [Fact]
    public void Get_GivesTheCraftItsGroupsAndRecipes_WithTheGraphicOfTheItem()
    {
        var result = Run(
            """
            local c = craft.get("carpentry")
            local lute = c.groups[1].recipes[1]
            return c.id, c.name, c.skill, c.sound, #c.groups, c.groups[1].name, lute.name, lute.item, lute.graphic,
                   lute.skill_min, lute.skill_max, #lute.resources, lute.resources[2].resource, lute.resources[2].amount,
                   lute.skills[1].skill, lute.skills[1].min, lute.skills[1].max
            """
        );

        Assert.Equal(
            ["carpentry", "Carpentry", "carpentry"],
            result[..3].Select(value => value.Read<string>())
        );
        Assert.Equal(0x023D, result[3].Read<int>());
        Assert.Equal(1, result[4].Read<int>());
        Assert.Equal(("Musical items", "Lute", "0x0eb3_lute"), (result[5].Read<string>(), result[6].Read<string>(), result[7].Read<string>()));
        Assert.Equal(0x0EB3, result[8].Read<int>());
        Assert.Equal((68.4, 93.4), (result[9].Read<double>(), result[10].Read<double>()));
        Assert.Equal((2, "cloth", 10), (result[11].Read<int>(), result[12].Read<string>(), result[13].Read<int>()));
        Assert.Equal(("musicianship", 45.0, 70.0), (result[14].Read<string>(), result[15].Read<double>(), result[16].Read<double>()));
    }

    [Fact]
    public void AnUnknownCraftOrList_IsNil_AKnownListGivesItsTemplates()
    {
        var result = Run("""return craft.get("tailoring"), craft.resource("gems"), #craft.resource("wood"), craft.resource("wood")[2]""");

        Assert.Equal(LuaValue.Nil, result[0]);
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal((2, "0x1bda_board"), (result[2].Read<int>(), result[3].Read<string>()));
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new CraftModule(_crafts, _templates));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
