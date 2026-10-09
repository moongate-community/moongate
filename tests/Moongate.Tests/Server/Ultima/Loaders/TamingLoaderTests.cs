using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class TamingLoaderTests
{
    private const string Horse = "[[creature]]\ntemplate = \"horse\"\nmin_skill = 29.1\nslots = 1\n";

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsTheCreatures()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/taming.toml", Horse + "[[creature]]\ntemplate = \"llama\"\nmin_skill = 80\nslots = 2\n");

        var creatures = (await CreateLoader(root).LoadDataAsync()).Entities.ToArray();

        Assert.Equal(
            [("horse", 29.1, 1), ("llama", 80.0, 2)],
            creatures.Select(creature => (creature.Template, creature.MinSkill, creature.Slots))
        );
    }

    [Fact]
    public async Task LoadDataAsync_SlotsLeftOut_AreOne()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/taming.toml", "[[creature]]\ntemplate = \"horse\"\nmin_skill = 10\n");

        var creature = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal(1, creature.Slots);
    }

    [Fact]
    public async Task LoadDataAsync_NoFile_LoadsNothing()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory]
    [InlineData("template = \"dragonfly\"\nmin_skill = 10", "dragonfly")]
    [InlineData("template = \"horse\"\nmin_skill = -1", "minimum skill")]
    [InlineData("template = \"horse\"\nmin_skill = 120.5", "minimum skill")]
    [InlineData("template = \"horse\"\nmin_skill = 10\nslots = 0", "slots")]
    [InlineData("template = \"horse\"\nmin_skill = 10\nslots = 11", "slots")]
    public async Task LoadDataAsync_ABadCreature_StopsTheServerNamingIt(string body, string mentions)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/taming.toml", "[[creature]]\n" + body + "\n");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains(mentions, error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_ACreatureTwice_StopsTheServer()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/taming.toml", Horse + Horse);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("twice", error.Message);
    }

    private static TamingLoader CreateLoader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["data"]),
            new StubDataLoaderService().With(
                new MobileTemplate { Id = "horse" },
                new MobileTemplate { Id = "llama" }
            )
        );
    }
}
