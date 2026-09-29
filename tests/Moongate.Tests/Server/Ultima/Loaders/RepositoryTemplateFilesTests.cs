using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Loads the templates shipped in <c>moongate_root/templates</c> with the real loaders, so a broken template fails
///     here instead of at the next server start.
/// </summary>
public sealed class RepositoryTemplateFilesTests
{
    [Fact]
    public async Task ShippedMotd_Loads()
    {
        var registry = new MotdVariableRegistry();
        MotdRenderer.RegisterBuiltins(registry);
        var loader = new MotdLoader(Directories(), registry);

        Assert.NotEmpty((await loader.LoadDataAsync()).Entities);
    }

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

        // Only abstract base templates may resolve to no graphic; anything else would spawn invisible.
        Assert.Empty(templates.Values.Where(t => t.ItemId.Value == 0 && !t.Id.Contains("base")).Select(t => t.Id));
    }

    [Fact]
    public async Task ShippedItemTemplates_MarkTwoHandedWeaponsButNotShieldsOrTorches()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        // Bows, polearms, staves: both hands.
        Assert.True(templates["0x13b2"].TwoHandedWeapon);
        Assert.True(templates["base_halberd"].TwoHandedWeapon);
        Assert.True(templates["base_quarter_staff"].TwoHandedWeapon);

        // Shields and a torch share the layer but leave the other hand free.
        Assert.Null(templates["base_heater_shield"].TwoHandedWeapon);
        Assert.Null(templates["0x0f64_torch"].TwoHandedWeapon);
        Assert.All(
            templates.Values.Where(t => t.Name?.EndsWith("shield", StringComparison.Ordinal) == true && t.Layer == LayerType.TwoHanded),
            shield => Assert.Null(shield.TwoHandedWeapon)
        );
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
        var common = Assert.Single(sets, set => set.Common);
        var gold = Assert.Single(common.Items, entry => entry.Items.SequenceEqual([new ItemsConfig().GoldTemplate]));
        Assert.Equal(1000, gold.Amount!.Value.Roll());
        Assert.False(gold.Equip);
        Assert.Contains(templates, t => t.Id == new ItemsConfig().BackpackTemplate);
        Assert.Contains(templates, t => t.Id == new ItemsConfig().GoldTemplate);
    }

    [Fact]
    public async Task ShippedMobileTemplates_LoadAgainstTheShippedNamesAndItems()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities.ToArray();
        var loader = new MobileTemplatesLoader(directories, new StubDataLoaderService().With(names).With(items).With(loots));

        var mobiles = (await loader.LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(671, mobiles.Count);
        Assert.Equal("{gender}", mobiles["guard"].NameList);
    }

    [Fact]
    public async Task ShippedGuardAndOrc_CreateWithTheShippedRacesAndNames()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var races = (await new RacesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(directories, new StubDataLoaderService().With(names).With(items).With(loots))
                       .LoadDataAsync()).Entities.ToArray();
        var loaders = new StubDataLoaderService().With(names).With(races).With(mobiles);
        var factory = new MobileFactoryService(
            new MobileTemplateService(loaders), new NameService(loaders), loaders, null!, null!, null!, null!, null!, null!, null!, null!, null!
        );

        var guard = factory.Create("guard");
        var orc = factory.Create("orc");

        Assert.Contains(guard.Body, new[] { 400, 401 });
        Assert.False(string.IsNullOrWhiteSpace(guard.Name));
        Assert.True(orc.Body > 0 && orc.HitsMax > 0 && !string.IsNullOrWhiteSpace(orc.Name), $"{orc.Body} {orc.Name}");
    }

    [Fact]
    public async Task ShippedLootTables_LoadAgainstTheShippedItems()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();

        var tables = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities;

        Assert.Equal(71, tables.Count);
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
