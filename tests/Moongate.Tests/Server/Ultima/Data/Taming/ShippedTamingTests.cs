using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Data.Taming;

/// <summary>
///     Loads the shipped <c>data/taming.toml</c> with the real loaders against the real mobile templates.
/// </summary>
public sealed class ShippedTamingTests
{
    [Fact]
    public async Task TheShippedFile_Loads_AndNamesTheClassicCreatures()
    {
        var (creatures, _) = await LoadAsync();

        Assert.True(creatures.Count > 50);
        Assert.Equal((29.1, 1), (creatures["horse"].MinSkill, creatures["horse"].Slots));
        Assert.Equal((95.1, 2), (creatures["nightmare"].MinSkill, creatures["nightmare"].Slots));
        Assert.True(creatures["cat"].MinSkill < 0);
    }

    [Theory,
     InlineData("horse"),
     InlineData("brownhorse"),
     InlineData("grayhorse"),
     InlineData("darkhorse"),
     InlineData("llama"),
     InlineData("desertostard"),
     InlineData("frenziedostard"),
     InlineData("forestostard")]
    public async Task ACreatureThatCanBeRidden_CanBeTamed(string template)
    {
        var (creatures, mobiles) = await LoadAsync();

        Assert.True(mobiles[template].Tags!.ContainsKey(MountProps.MountItemTag));
        Assert.True(creatures.ContainsKey(template));
    }

    [Fact]
    public async Task EveryTamableCreature_HasACreatureScript_ToFollowItsOwnerWith()
    {
        var (creatures, mobiles) = await LoadAsync();

        var without = creatures.Keys
            .Where(template => mobiles[template].ScriptId is not ("animal" or "scared_animal" or "monster"))
            .Order()
            .ToList();

        Assert.True(without.Count == 0, "No creature script: " + string.Join(", ", without));
    }

    private static async Task<(Dictionary<string, TamingCreature> Creatures, Dictionary<string, MobileTemplate> Mobiles)> LoadAsync()
    {
        var directories = new DirectoriesConfig(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data", "templates"]);
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            )
            .LoadDataAsync()).Entities.ToArray();
        var creatures = (await new TamingLoader(directories, new StubDataLoaderService().With(mobiles)).LoadDataAsync())
            .Entities.ToArray();

        return (creatures.ToDictionary(creature => creature.Template), mobiles.ToDictionary(mobile => mobile.Id));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Moongate.slnx not found above the test output.");
    }
}
