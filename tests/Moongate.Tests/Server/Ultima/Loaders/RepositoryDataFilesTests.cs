using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Messages;
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
        container.RegisterInstance(new LocalizationConfig { Language = "ita" });
        container.Register<IDataLoaderService, DataLoaderService>(Reuse.Singleton);
        var service = container.Resolve<IDataLoaderService>();

        await service.StartAsync();

        Assert.Equal(6, service.GetEntities<MapContent>().Count);
        Assert.Equal(10, service.GetEntities<StartingCityContent>().Count);
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

        var messages = service.GetEntities<MessageContent>();
        Assert.Equal(5462, messages.Count);
        Assert.Equal("Si sale a bordo della barca.", messages.Single(message => message.Id == 1).Text);
        Assert.Equal("[{0:x} {1:x} {2:x} {3:x}]", messages.Single(message => message.Id == 1737).Text);

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

        Assert.Equal(5462, (await loader.LoadDataAsync()).Entities.Count);
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
