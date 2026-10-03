using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal;
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

    [Fact]
    public async Task ShippedTitles_LoadCompleteClassicGrid()
    {
        var rows = (await new TitlesLoader(Directories()).LoadDataAsync()).Entities;

        Assert.Equal(55, rows.Count);
        Assert.Contains(rows, row => row.Fame == 0 && row.Karma == -15000 && row.Title == "The Outcast");
        Assert.Contains(rows, row => row.Fame == 10000 && row.Karma == 10000 &&
                                     row.Title == "The Glorious Lord" && row.FemaleTitle == "The Glorious Lady");
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
    public async Task ShippedDecorationTemplates_AreFixedAndDoNotDecay_AndTheDoorHasTheDoorScript()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var decoration = templates["decoration"];
        var door = templates["decoration_door"];
        Assert.Equal(((bool?)false, (bool?)false, (string?)null), (decoration.Movable, decoration.Decays, decoration.Name));
        Assert.True(string.IsNullOrEmpty(decoration.ScriptId));
        Assert.Equal(((bool?)false, (bool?)false, "door"), (door.Movable, door.Decays, door.ScriptId));
        var light = templates["decoration_light"];
        Assert.Equal(((bool?)false, (bool?)false, "light"), (light.Movable, light.Decays, light.ScriptId));
        Assert.Equal("light", templates["0x0a28_candle"].ScriptId);
        Assert.True(templates.ContainsKey(Moongate.Server.Ultima.Commands.KeyCommand.KeyTemplate));
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

        Assert.Equal(674, mobiles.Count);
        Assert.Equal("{gender}", mobiles["guard"].NameList);
        // Moongate's own cats inherit the UOX3 cat and add their name and script.
        Assert.Equal((201, "Orione", "orione"), (mobiles["orione"].Body, mobiles["orione"].Name, mobiles["orione"].ScriptId));
        Assert.Equal((201, "Vega", "vega"), (mobiles["vega"].Body, mobiles["vega"].Name, mobiles["vega"].ScriptId));
        var lilly = mobiles["lilly"];
        Assert.Equal(("Lilly", "the Noble", 32000, 32000), (lilly.Name, lilly.Title, lilly.Fame!.Value.Roll(), lilly.Karma!.Value.Roll()));
        Assert.Equal(
            ["0x230e_gilded_dress", "0x1711_thigh_boots", "base_royal_circlet"],
            lilly.Equipment!.SelectMany(entry => entry.Items)
        );
    }

    [Fact]
    public async Task ShippedBankers_AllHaveTheBankerScript()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities.ToArray();
        var loader = new MobileTemplatesLoader(directories, new StubDataLoaderService().With(names).With(items).With(loots));

        var bankers = (await loader.LoadDataAsync()).Entities
            .Where(template => template.Id.EndsWith("banker", StringComparison.Ordinal))
            .ToDictionary(template => template.Id, template => template.ScriptId);

        Assert.Equal(
            ["banker", "f_banker", "f_gypsybanker", "gypsybanker", "m_banker", "m_gypsybanker"],
            bankers.Keys.Order(StringComparer.Ordinal)
        );
        Assert.All(bankers.Values, script => Assert.Equal("banker", script));
    }

    [Fact]
    public async Task ShippedNpcListsAndSpawns_LoadAgainstTheShippedMobiles()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(directories, new StubDataLoaderService().With(names).With(items).With(loots))
                          .LoadDataAsync()).Entities.ToArray();
        var lists = (await new NpcListsLoader(directories, new StubDataLoaderService().With(mobiles)).LoadDataAsync()).Entities.ToArray();

        var spawns = (await new SpawnsLoader(directories, new StubDataLoaderService().With(mobiles).With(lists)).LoadDataAsync())
                     .Entities.ToDictionary(spawn => spawn.Id);

        Assert.Equal(446, lists.Length);
        Assert.Equal(4041, spawns.Count);
        var shop = spawns["felucca_0"];
        Assert.Equal(("The Hammer And Anvil", MapType.Felucca, 480), (shop.Name, shop.Map, shop.MinMinutes));
        Assert.Equal(["weaponsmith"], shop.MobileIds);
    }

    [Fact]
    public async Task ShippedGumps_LoadAndTheDecorationConfirmationOffersConfirmAndCancel()
    {
        var gumps = (await new GumpsLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(gump => gump.Id);

        var rendered = GumpXmlRenderer.Render(gumps[DecorateCommand.ConfirmGump], new Dictionary<string, string>(), null);

        Assert.Equal(["cancel", DecorateCommand.ConfirmClick], rendered.Clicks.Values.Order());
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

        var lilly = factory.Create("lilly");
        Assert.Equal((401, "Lilly", 32000, 32000), (lilly.Body, lilly.Name, lilly.Fame, lilly.Karma));
    }

    [Fact]
    public async Task ShippedTreasureChests_AreFixedDecayAndHoldGoldAndLoot()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var chests = Enumerable.Range(1, 4).Select(level => templates[$"treasure_chest_level_{level}"]).ToList();

        Assert.All(
            chests,
            chest => Assert.Equal(((bool?)false, (bool?)true, (int?)45), (chest.Movable, chest.Decays, chest.DecayMinutes))
        );
        Assert.Equal([30, 70, 180, 200], chests.Select(chest => chest.Gold!.Value.Min));
        Assert.Equal([129, 169, 419, 599], chests.Select(chest => chest.Gold!.Value.Max));
        Assert.Equal([5, 5, 12, 19], chests.Select(chest => chest.Loot!.Count));
        Assert.Equal([0x0E43u, 0x0E41u, 0x09ABu, 0x0E40u], chests.Select(chest => chest.ItemId.Value));
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
