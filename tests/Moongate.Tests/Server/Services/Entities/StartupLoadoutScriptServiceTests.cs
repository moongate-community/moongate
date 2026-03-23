using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Entities;
using Moongate.Server.Services.Entities;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Entities;

public sealed class StartupLoadoutScriptServiceTests
{
    [Test]
    public void BuildLoadout_WhenLuaHookExists_ShouldReturnParsedLoadout()
    {
        using var temp = new TempDirectory();
        using var engine = CreateScriptEngine(temp.Path);
        engine.ExecuteScript(
            """
            function build_starting_loadout(context)
                return {
                    backpack = {
                        {
                            template_id = "Gold",
                            amount = 1000
                        },
                        {
                            template_id = "Spellbook",
                            args = {
                                title = "Arcane Notes",
                                author = context.player_name,
                                pages = 32,
                                writable = true
                            }
                        }
                    },
                    equip = {
                        {
                            template_id = "Shoes",
                            layer = "Shoes"
                        }
                    }
                }
            end
            """
        );

        var service = new StartupLoadoutScriptService(engine);
        var loadout = service.BuildLoadout(CreateContext("Mage", GenderType.Female, new TestRace("elf", 1)), "Lyra");

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Backpack, Has.Count.EqualTo(2));
                Assert.That(loadout.Backpack[0].TemplateId, Is.EqualTo("Gold"));
                Assert.That(loadout.Backpack[0].Amount, Is.EqualTo(1000));
                Assert.That(loadout.Backpack[1].TemplateId, Is.EqualTo("Spellbook"));
                Assert.That(loadout.Backpack[1].Args.HasValue, Is.True);
                Assert.That(loadout.Backpack[1].Args!.Value.GetProperty("author").GetString(), Is.EqualTo("Lyra"));
                Assert.That(loadout.Equip, Has.Count.EqualTo(1));
                Assert.That(loadout.Equip[0].TemplateId, Is.EqualTo("Shoes"));
                Assert.That(loadout.Equip[0].Layer, Is.EqualTo(ItemLayerType.Shoes));
            }
        );
    }

    [Test]
    public void BuildLoadout_WhenLuaHookIsMissing_ShouldReturnEmptyLoadout()
    {
        using var temp = new TempDirectory();
        using var engine = CreateScriptEngine(temp.Path);
        var service = new StartupLoadoutScriptService(engine);

        var loadout = service.BuildLoadout(CreateContext("Warrior", GenderType.Male), "Tester");

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Backpack, Is.Empty);
                Assert.That(loadout.Equip, Is.Empty);
            }
        );
    }

    private static StarterProfileContext CreateContext(string professionName, GenderType gender, Race? race = null)
    {
        var profession = new ProfessionInfo
        {
            ID = 1,
            Name = professionName
        };

        return new(profession, race, gender);
    }

    private static LuaScriptEngineService CreateScriptEngine(string rootPath)
    {
        var directories = new DirectoriesConfig(rootPath, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);

        return new(
            directories,
            [],
            new Container(),
            new(rootPath, scriptsDirectory, "test"),
            []
        );
    }
}
