using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Loads the templates shipped in <c>moongate_root/templates</c> with the real loaders, so a broken template fails
///     here instead of at the next server start.
/// </summary>
public sealed class RepositoryTemplateFilesTests
{
    public RepositoryTemplateFilesTests()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public async Task ShippedItemTemplates_LoadAndResolve()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.True(templates.Count > 7000, $"only {templates.Count} templates");
        Assert.Equal(0x0E75u, templates["0x0e75_backpack"].ItemId.Value);
        Assert.True(templates["0x0eed_gold_coin"].Stackable);
    }

    [Fact]
    public async Task ShippedStartingItems_ResolveAgainstTheShippedTemplates()
    {
        var directories = Directories();
        var templates = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loader = new StartingItemsLoader(directories, new StubDataLoaderService().With(templates));
        await loader.InitializeAsync();

        var sets = (await loader.LoadDataAsync()).Entities;

        Assert.Equal(56, sets.Count);
        Assert.Single(sets, set => set.Common);
        Assert.Contains(templates, t => t.Id == new StartingItemsConfig().BackpackTemplate);
        Assert.Contains(templates, t => t.Id == new StartingItemsConfig().GoldTemplate);
    }

    private static DirectoriesConfig Directories()
    {
        return new(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data", "templates"]);
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
