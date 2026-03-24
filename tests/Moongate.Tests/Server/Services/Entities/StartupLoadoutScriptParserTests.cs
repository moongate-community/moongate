using Moongate.Server.Services.Entities;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Entities;

public sealed class StartupLoadoutScriptParserTests
{
    [Test]
    public void Parse_WhenAmountIsNotPositive_ShouldThrow()
    {
        var script = new Script();
        var result = new Table(script)
        {
            ["backpack"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["template_id"] = "Gold",
                    ["amount"] = 0
                }
            }
        };

        Assert.That(
            () => StartupLoadoutScriptResultParser.Parse(result),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("amount")
        );
    }

    [Test]
    public void Parse_WhenArgsIsNotTable_ShouldThrow()
    {
        var script = new Script();
        var result = new Table(script)
        {
            ["equip"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["template_id"] = "Spellbook",
                    ["args"] = "bad"
                }
            }
        };

        Assert.That(
            () => StartupLoadoutScriptResultParser.Parse(result),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("args")
        );
    }

    [Test]
    public void Parse_WhenBackpackAndEquipEntriesAreValid_ShouldReturnLoadout()
    {
        var script = new Script();
        var result = new Table(script)
        {
            ["backpack"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["template_id"] = "Gold",
                    ["amount"] = 1000
                }
            },
            ["equip"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["template_id"] = "Spellbook",
                    ["layer"] = "OneHanded",
                    ["hue"] = 0x0455,
                    ["args"] = new Table(script)
                    {
                        ["title"] = "Arcane Notes",
                        ["author"] = "Tester",
                        ["pages"] = 32,
                        ["writable"] = true
                    }
                }
            }
        };

        var loadout = StartupLoadoutScriptResultParser.Parse(result);

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Backpack, Has.Count.EqualTo(1));
                Assert.That(loadout.Backpack[0].TemplateId, Is.EqualTo("Gold"));
                Assert.That(loadout.Backpack[0].Amount, Is.EqualTo(1000));
                Assert.That(loadout.Equip, Has.Count.EqualTo(1));
                Assert.That(loadout.Equip[0].TemplateId, Is.EqualTo("Spellbook"));
                Assert.That(loadout.Equip[0].Layer, Is.EqualTo(ItemLayerType.OneHanded));
                Assert.That(loadout.Equip[0].Hue, Is.EqualTo(0x0455));
                Assert.That(loadout.Equip[0].Args.HasValue, Is.True);
                Assert.That(loadout.Equip[0].Args!.Value.GetProperty("author").GetString(), Is.EqualTo("Tester"));
                Assert.That(loadout.Equip[0].Args!.Value.GetProperty("pages").GetInt32(), Is.EqualTo(32));
            }
        );
    }

    [Test]
    public void Parse_WhenEquipLayerIsInvalid_ShouldThrow()
    {
        var script = new Script();
        var result = new Table(script)
        {
            ["equip"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["template_id"] = "Shoes",
                    ["layer"] = "BadLayer"
                }
            }
        };

        Assert.That(
            () => StartupLoadoutScriptResultParser.Parse(result),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("layer")
        );
    }

    [Test]
    public void Parse_WhenHueIsInvalid_ShouldThrow()
    {
        var script = new Script();
        var result = new Table(script)
        {
            ["equip"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["template_id"] = "Shoes",
                    ["layer"] = "Shoes",
                    ["hue"] = "bad"
                }
            }
        };

        Assert.That(
            () => StartupLoadoutScriptResultParser.Parse(result),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("hue")
        );
    }

    [Test]
    public void Parse_WhenSectionsAreMissing_ShouldReturnEmptyLoadout()
    {
        var script = new Script();
        var result = new Table(script);

        var loadout = StartupLoadoutScriptResultParser.Parse(result);

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Backpack, Is.Empty);
                Assert.That(loadout.Equip, Is.Empty);
            }
        );
    }

    [Test]
    public void Parse_WhenTemplateIdIsMissing_ShouldThrow()
    {
        var script = new Script();
        var result = new Table(script)
        {
            ["backpack"] = new Table(script)
            {
                [1] = new Table(script)
                {
                    ["amount"] = 1
                }
            }
        };

        Assert.That(
            () => StartupLoadoutScriptResultParser.Parse(result),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("template_id")
        );
    }
}
