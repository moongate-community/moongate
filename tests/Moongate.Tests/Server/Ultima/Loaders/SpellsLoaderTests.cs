using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class SpellsLoaderTests
{
    private const string Clumsy = """
        [[spell]]
        id = 1
        key = "clumsy"
        name = "Clumsy"
        circle = 1
        mantra = "Uus Jux"
        action = 17
        reagents = [{ template = "blackpearl", amount = 2 }]
        target = "mobile"
        harmful = true
        resistable = true
        reflectable = true
        sound = 0x1DF
        effect = 0x3779
        effect_duration = 15
        prompt = "Select target for clumsy."
        scroll = "clumsyscroll"
        enabled = true

        """;

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsTheSpell()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/spells.toml", Clumsy);

        var spell = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal((1, "clumsy", "Clumsy", 1, "Uus Jux", 17), (spell.Id, spell.Key, spell.Name, spell.Circle, spell.Mantra, spell.Action));
        Assert.Equal(SpellTargetType.Mobile, spell.Target);
        Assert.True(spell.Harmful && spell.Resistable && spell.Reflectable && spell.Enabled);
        Assert.Equal((0x1DF, 0x3779, 15), (spell.Sound, spell.Effect, spell.EffectDuration));
        var reagent = Assert.Single(spell.Reagents);
        Assert.Equal(("blackpearl", 2), (reagent.Template, reagent.Amount));
        Assert.Equal("clumsyscroll", spell.Scroll);
        Assert.Equal(1UL, spell.Bit);
    }

    [Fact]
    public async Task LoadDataAsync_TheCastDelayScale_IsOneUnlessTheSpellSaysOtherwise()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/spells.toml", Clumsy + Clumsy.Replace("id = 1", "id = 2").Replace("\"clumsy\"", "\"slow\"") + "cast_delay_scale = 4\n");

        var spells = (await CreateLoader(root).LoadDataAsync()).Entities.OrderBy(spell => spell.Id).ToList();

        Assert.Equal([1.0, 4.0], spells.Select(spell => spell.CastDelayScale));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("11")]
    public async Task LoadDataAsync_ACastDelayScaleOutOfZeroToTen_StopsTheServerNamingIt(string scale)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/spells.toml", Clumsy + $"cast_delay_scale = {scale}\n");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("cast_delay_scale", error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_NoFile_LoadsNothing()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory]
    [InlineData("id = 0", "number")]
    [InlineData("id = 65", "number")]
    [InlineData("key = \"Clumsy\"", "key")]
    [InlineData("circle = 0", "circle")]
    [InlineData("circle = 9", "circle")]
    [InlineData("reagents = [{ template = \"mandrake\", amount = 1 }]", "mandrake")]
    [InlineData("reagents = [{ template = \"blackpearl\", amount = 0 }]", "amount")]
    [InlineData("scroll = \"nothing\"", "nothing")]
    public async Task LoadDataAsync_ABadSpell_StopsTheServerNamingIt(string replacement, string mentions)
    {
        using var root = new TemporaryDirectory();
        var field = replacement[..replacement.IndexOf(' ')];
        var lines = Clumsy.Split('\n').Select(line => line.StartsWith(field + " ", StringComparison.Ordinal) ? replacement : line);
        root.CreateFile("data/spells.toml", string.Join('\n', lines));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains(mentions, error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_ASpellNumberTwice_StopsTheServer()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/spells.toml", Clumsy + Clumsy.Replace("key = \"clumsy\"", "key = \"other\""));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("twice", error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AKeyTwice_StopsTheServer()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/spells.toml", Clumsy + Clumsy.Replace("id = 1", "id = 2"));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("twice", error.Message);
    }

    private static SpellsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["data"]),
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "blackpearl" },
                new ItemTemplate { Id = "clumsyscroll" }
            )
        );
    }
}
