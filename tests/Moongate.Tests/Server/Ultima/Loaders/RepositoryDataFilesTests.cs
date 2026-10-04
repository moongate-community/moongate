using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Loads the data files shipped in <c>moongate_root/data</c> with the real loaders, in the server's order, so a
///     broken file fails here instead of at the next server start.
/// </summary>
public sealed class RepositoryDataFilesTests
{
    [Fact]
    public async Task ShippedDataFiles_LoadWithTheRealLoaders()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new Point2DTomlConverter());
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new Rectangle2DTomlConverter());
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data"]));
        container.AddUltimaDataLoader<MapLoader, MapContent>(0);
        container.AddUltimaDataLoader<StartingCitiesLoader, StartingCityContent>(1);
        container.AddUltimaDataLoader<SkillsLoader, SkillContent>(2);
        container.AddUltimaDataLoader<ProfessionsLoader, ProfessionContent>(3);
        container.AddUltimaDataLoader<RacesLoader, RaceContent>(4);
        container.AddUltimaDataLoader<BannedNamesLoader, BannedNamesContent>(5);
        container.AddUltimaDataLoader<ContainersLoader, ContainerContent>(6);
        container.AddUltimaDataLoader<BodiesLoader, BodyContent>(7);
        container.AddUltimaDataLoader<WeatherLoader, WeatherContent>(8);
        container.AddUltimaDataLoader<RegionsLoader, RegionContent>(9);
        container.AddUltimaDataLoader<MessagesLoader, MessageContent>(10);
        container.AddUltimaDataLoader<NamesLoader, NameList>(11);
        container.AddUltimaDataLoader<MoongatesLoader, MoongateFacet>(12);
        container.AddUltimaDataLoader<LocationsLoader, NamedLocation>(13);
        container.RegisterInstance(new LocalizationConfig { Language = "ita" });
        container.Register<IDataLoaderService, DataLoaderService>(Reuse.Singleton);
        var service = container.Resolve<IDataLoaderService>();

        await service.StartAsync();

        Assert.Equal(6, service.GetEntities<MapContent>().Count);
        Assert.Equal(10, service.GetEntities<StartingCityContent>().Count);
        var moongates = service.GetEntities<MoongateFacet>();
        Assert.Equal(
            [MapType.Trammel, MapType.Felucca, MapType.Ilshenar, MapType.Malas, MapType.Tokuno, MapType.TerMur],
            moongates.Select(facet => facet.Map)
        );
        Assert.Equal([9, 9, 9, 2, 3, 2], moongates.Select(facet => facet.Destination.Count));
        var places = service.GetEntities<NamedLocation>();
        Assert.Equal(558, places.Count);
        Assert.Equal(
            [MapType.Felucca, MapType.Trammel, MapType.Ilshenar, MapType.Malas, MapType.Tokuno, MapType.TerMur],
            places.Select(place => place.Map).Distinct()
        );
        Assert.Equal(58, service.GetEntities<SkillContent>().Count);
        Assert.Equal(7, service.GetEntities<ProfessionContent>().Count);
        Assert.Equal(3, service.GetEntities<RaceContent>().Count);
        Assert.NotEmpty(Assert.Single(service.GetEntities<BannedNamesContent>()).Words);
        Assert.Single(service.GetEntities<ContainerContent>(), entry => entry.Default);
        Assert.Equal(1045, service.GetEntities<BodyContent>().Count);

        var names = service.GetEntities<NameList>();
        Assert.Contains(names, list => list.Id == "male" && list.Names.Count > 500);
        Assert.Contains(names, list => list.Id == "female" && list.Names.Count > 500);
        Assert.Equal("Aaron", names.Single(list => list.Id == "male").Names[0]);
        Assert.Equal(20, names.Count);
        Assert.Contains("a daemon", names.Single(list => list.Id == "daemon").Names);

        var messages = service.GetEntities<MessageContent>();
        Assert.Equal(5589, messages.Count);
        Assert.Equal("Si sale a bordo della barca.", messages.Single(message => message.Id == 1).Text);
        Assert.Equal("[{0:x} {1:x} {2:x} {3:x}]", messages.Single(message => message.Id == 1737).Text);
        Assert.Equal(
            ["Comune", "Non comune", "Raro", "Epico", "Leggendario"],
            new[] { 30000, 30001, 30002, 30003, 30004 }.Select(id => messages.Single(message => message.Id == id).Text)
        );

        var regions = service.GetEntities<RegionContent>();
        Assert.Equal(388, regions.Count);
        Assert.Equal(10, service.GetEntities<WeatherContent>().Count);
        Assert.Equal("temperate", service.GetEntities<MapContent>().Single(map => map.Map == MapType.Felucca).Weather);
        Assert.Equal("none", service.GetEntities<MapContent>().Single(map => map.Map == MapType.Malas).Weather);
        var britain = Assert.Single(regions, region => region.Map == MapType.Trammel && region.Name == "Britain");
        Assert.True(britain.Guarded);
        Assert.False(britain.Housing);
        Assert.Equal(MusicType.Britain1, britain.Music);
        Assert.Equal("temperate", britain.Weather);
        Assert.Contains(regions, region => region.Map == MapType.Felucca && region.Priority == 0 && region.Weather == "snowy" && region.Contains(4000, 300, 0));
        Assert.All(regions.Where(region => region.Type == RegionType.Dungeon), region => Assert.Equal("none", region.Weather));
        Assert.Equal(new Point3D(1495, 1629, 10), britain.GoLocation);
        Assert.True(britain.Contains(1495, 1629, 10));
        var lookup = new RegionService(service);
        Assert.Equal("Britain", lookup.Find(MapType.Trammel, new Point3D(1495, 1629, 10))?.Name);
        Assert.Equal("snowy", lookup.Find(MapType.Felucca, new Point3D(4000, 300, 0))?.Weather);
        Assert.All(regions.Where(region => region.Map == MapType.Ilshenar), region => Assert.False(region.RecallIn));

        // Travel zones: Felucca's Lost Lands block recalling out; Trammel's Wind allows it but blocks recalling in.
        var lostLands = regions.Where(region => region.Map == MapType.Felucca && region.Contains(5500, 3000, 0)).ToList();
        Assert.Contains(lostLands, region => !region.RecallOut);
        var trammelWind = regions.Where(region => region.Map == MapType.Trammel && region.Contains(5300, 100, 0)).ToList();
        Assert.Contains(trammelWind, region => !region.RecallIn);
        Assert.DoesNotContain(trammelWind, region => !region.RecallOut);
        Assert.Contains(trammelWind, region => region.Name == "Wind" && region.Guarded);
        var heartwood = Assert.Single(regions, region => region.Map == MapType.Felucca && region.Name == "The Heartwood");
        Assert.False(heartwood.TeleportIn);
        Assert.False(heartwood.TeleportOut);
        var bedlam = Assert.Single(regions, region => region.Map == MapType.Malas && region.Name == "Bedlam");
        Assert.False(bedlam.TeleportIn);
        Assert.True(bedlam.TeleportOut);
        Assert.True(britain.TeleportIn);
        var crystalCave = regions.Where(region => region.Map == MapType.Malas && region.Priority == 0 && region.Contains(1190, 450, -90));
        Assert.False(Assert.Single(crystalCave).TeleportOut);
        Assert.DoesNotContain(
            regions,
            region => region.Map == MapType.Malas && region.Priority == 0 && region.Contains(1190, 450, -70) && !region.RecallOut &&
                      region.Areas.Any(area => area.Z2 == -80)
        );
    }

    [Theory,
     InlineData("eng"), InlineData("ita"), InlineData("ger"), InlineData("fre"),
     InlineData("spa"), InlineData("por"), InlineData("pol"), InlineData("cze")]
    public async Task ShippedMessageFiles_LoadForEveryLanguage(string language)
    {
        var loader = new MessagesLoader(
            new DirectoriesConfig(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data"]),
            new LocalizationConfig { Language = language }
        );

        await loader.InitializeAsync();

        Assert.Equal(5589, (await loader.LoadDataAsync()).Entities.Count);
    }

    [Theory,
     InlineData("ita", "Comune", "Leggendario"), InlineData("ger", "Gewöhnlich", "Legendär"),
     InlineData("fre", "Commun", "Légendaire"), InlineData("spa", "Común", "Legendario"),
     InlineData("por", "Comum", "Lendário"), InlineData("pol", "Pospolity", "Legendarny"),
     InlineData("cze", "Běžný", "Legendární")]
    public async Task ShippedMessageFiles_TranslateTheRarities(string language, string common, string legendary)
    {
        var loader = new MessagesLoader(
            new DirectoriesConfig(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data"]),
            new LocalizationConfig { Language = language }
        );
        await loader.InitializeAsync();

        var messages = (await loader.LoadDataAsync()).Entities.ToDictionary(message => message.Id, message => message.Text);

        Assert.Equal((common, legendary), (messages[30000], messages[30004]));
    }

    [Theory,
     InlineData("eng", "[Cursed]", "Weight: {0} stones"), InlineData("ita", "[Maledetto]", "Peso: {0} pietre"),
     InlineData("ger", "[Verflucht]", "Gewicht: {0} Steine"), InlineData("fre", "[Maudit]", "Poids : {0} pierres"),
     InlineData("spa", "[Maldito]", "Peso: {0} piedras"), InlineData("por", "[Amaldiçoado]", "Peso: {0} pedras"),
     InlineData("pol", "[Przeklęty]", "Waga: {0} kam."), InlineData("cze", "[Prokletý]", "Váha: {0} kam.")]
    public async Task ShippedMessageFiles_TranslateTheTooltipTexts(string language, string cursed, string stones)
    {
        var loader = new MessagesLoader(
            new DirectoriesConfig(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data"]),
            new LocalizationConfig { Language = language }
        );
        await loader.InitializeAsync();

        var messages = (await loader.LoadDataAsync()).Entities.ToDictionary(message => message.Id, message => message.Text);

        Assert.Equal((cursed, stones), (messages[30005], messages[30007]));
        Assert.Contains("1", messages[30006]);
    }

    [Theory,
     InlineData("eng"), InlineData("ita"), InlineData("ger"), InlineData("fre"),
     InlineData("spa"), InlineData("por"), InlineData("pol"), InlineData("cze")]
    public async Task ShippedMessageFiles_HaveEveryCommandText(string language)
    {
        var directories = new DirectoriesConfig(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data"]);
        var own = Tomlyn.TomlSerializer.Deserialize<Tomlyn.Model.TomlTable>(
            await File.ReadAllTextAsync(Path.Combine(directories["data"], "messages", language, "moongate.toml"))
        )!;
        var messages = (Tomlyn.Model.TomlTable)own["messages"];

        // Every language carries its own text, not the English fallback.
        Assert.All(Enumerable.Range(30008, 47), id => Assert.True(messages.ContainsKey(id.ToString()), $"{language} lacks {id}"));
    }

    [Theory,
     InlineData("eng"), InlineData("ita"), InlineData("ger"), InlineData("fre"),
     InlineData("spa"), InlineData("por"), InlineData("pol"), InlineData("cze")]
    public async Task ShippedMessageFiles_KeepMoongateTextsApartFromTheStandardOnes(string language)
    {
        var messagesDirectory = Path.Combine(FindRepositoryRoot(), "moongate_root", "data", "messages");
        var standard = await ReadMessageIdsAsync(Path.Combine(messagesDirectory, language + ".toml"));
        var moongate = await ReadMessageIdsAsync(Path.Combine(messagesDirectory, language, "moongate.toml"));

        Assert.NotEmpty(standard);
        Assert.NotEmpty(moongate);
        Assert.All(standard, id => Assert.True(id < 30000, $"{language}.toml holds the Moongate text {id}"));
        Assert.All(moongate, id => Assert.True(id >= 30000, $"{language}/moongate.toml holds the standard text {id}"));
    }

    private static async Task<List<int>> ReadMessageIdsAsync(string path)
    {
        var file = Tomlyn.TomlSerializer.Deserialize<Tomlyn.Model.TomlTable>(await File.ReadAllTextAsync(path))!;

        return ((Tomlyn.Model.TomlTable)file["messages"]).Keys.Select(int.Parse).ToList();
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
