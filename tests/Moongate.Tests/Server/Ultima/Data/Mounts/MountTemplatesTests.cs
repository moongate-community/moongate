using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Data.Mounts;

/// <summary>
///     Loads the shipped templates with the real loaders and checks which creatures turn into which mount item.
/// </summary>
public sealed class MountTemplatesTests
{
    [Theory]
    [InlineData("horse", "horse4")]
    [InlineData("brownhorse", "horse2")]
    [InlineData("grayhorse", "horse3")]
    [InlineData("darkhorse", "horse1")]
    [InlineData("llama", "llama")]
    [InlineData("desertostard", "desertostard")]
    [InlineData("frenziedostard", "frenziedostard")]
    [InlineData("forestostard", "forestostard")]
    public async Task Creature_ThatCanBeRidden_NamesItsMountItem(string creature, string item)
    {
        var (mobiles, _) = await LoadAsync();

        Assert.Equal(item, mobiles[creature].Tags![MountProps.MountItemTag]);
    }

    [Theory]
    [InlineData("etherealhorse")]
    [InlineData("nightmare")]
    [InlineData("darknightmare")]
    [InlineData("unicorn")]
    [InlineData("swampdragon")]
    [InlineData("etherealkirin")]
    public async Task Creature_NotRiddenYet_NamesNoMountItem(string creature)
    {
        var (mobiles, _) = await LoadAsync();

        Assert.True(
            mobiles[creature].Tags is null ||
            !mobiles[creature].Tags!.TryGetValue(MountProps.MountItemTag, out var item) ||
            item.Length == 0
        );
    }

    [Fact]
    public async Task EveryEtherealStatuette_NamesAnExistingMountItemOnTheMountLayer_AndUsesTheScript()
    {
        var (_, items) = await LoadAsync();
        var statuettes = items.Values.Where(item => item.ScriptId == "ethereal_mount").ToList();

        Assert.Equal(8, statuettes.Count);
        Assert.All(
            statuettes,
            statuette =>
            {
                Assert.True(items.TryGetValue(statuette.Tags![MountProps.MountItemTag], out var mount), statuette.Id);
                Assert.Equal(LayerType.Mount, mount!.Layer);
            }
        );
    }

    [Fact]
    public async Task EveryMountItemTag_PointsToAnItemOnTheMountLayer()
    {
        var (mobiles, items) = await LoadAsync();
        var tagged = mobiles.Values
            .Where(mobile => mobile.Tags?.GetValueOrDefault(MountProps.MountItemTag) is { Length: > 0 })
            .ToList();

        Assert.NotEmpty(tagged);
        Assert.All(
            tagged,
            mobile =>
            {
                Assert.True(items.TryGetValue(mobile.Tags![MountProps.MountItemTag], out var item), mobile.Id);
                Assert.Equal(LayerType.Mount, item!.Layer);
            }
        );
    }

    private static async Task<(Dictionary<string, MobileTemplate> Mobiles, Dictionary<string, ItemTemplate> Items)> LoadAsync()
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

        return (mobiles.ToDictionary(mobile => mobile.Id), items.ToDictionary(item => item.Id));
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
