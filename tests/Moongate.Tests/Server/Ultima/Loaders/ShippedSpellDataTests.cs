using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Loads <c>moongate_root/data/spells.toml</c> with the real loaders over the shipped item templates, so a spell that
///     names a reagent or a scroll that is gone fails here instead of at the next server start.
/// </summary>
public sealed class ShippedSpellDataTests
{
    [Fact]
    public async Task ShippedSpells_Are64_AndEveryReagentAndScrollIsAShippedTemplate()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();

        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities;

        Assert.Equal(Enumerable.Range(1, 64), spells.Select(spell => spell.Id).Order());
        Assert.Equal(8, spells.Count(spell => spell.Circle == 1));
        Assert.Equal(spells.Count, spells.Select(spell => spell.Scroll).Distinct().Count());
    }

    [Fact]
    public async Task ShippedSpells_FirstCircle_AreWhatTheClientCalls()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities;

        Assert.Equal(
            ["clumsy", "create_food", "feeblemind", "heal", "magic_arrow", "night_sight", "reactive_armor", "weaken"],
            spells.Where(spell => spell.Circle == 1).OrderBy(spell => spell.Id).Select(spell => spell.Key)
        );
        var arrow = spells.Single(spell => spell.Key == "magic_arrow");
        Assert.Equal(("In Por Ylem", SpellTargetType.Mobile, true), (arrow.Mantra, arrow.Target, arrow.Harmful));
        Assert.Equal("0x0f8c_sulfurous_ash", Assert.Single(arrow.Reagents).Template);
        Assert.Equal(SpellTargetType.None, spells.Single(spell => spell.Key == "create_food").Target);
    }

    [Fact]
    public async Task EverySpellScript_IsASpellOfTheData_AndEveryScrollAndBookHasItsScript()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities;
        var root = Path.Combine(directories["data"], "..");
        var scripts = Directory.GetFiles(Path.Combine(root, "scripts", "spells"), "*.lua")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();
        var templates = items.ToDictionary(item => item.Id);

        Assert.NotEmpty(scripts);
        Assert.All(scripts, key => Assert.Contains(spells, spell => spell.Key == key));
        Assert.All(spells, spell => Assert.Equal("spell_scroll", templates[spell.Scroll].ScriptId));
        Assert.All(
            items.Where(item => item.Id.StartsWith("spellbook", StringComparison.Ordinal) || item.Id == "0x0efa_a_spellbook"),
            item => Assert.Equal("spellbook", item.ScriptId)
        );
        Assert.Equal(ulong.MaxValue.ToString(), templates["spellbook_full"].Tags!["spells"]);
        Assert.Equal("255", templates["spellbook1"].Tags!["spells"]);
        Assert.Null(templates["spellbook"].Tags);
    }

    private static DirectoriesConfig Directories()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return new(Path.Combine(directory!.FullName, "moongate_root"), ["data", "templates"]);
    }
}
