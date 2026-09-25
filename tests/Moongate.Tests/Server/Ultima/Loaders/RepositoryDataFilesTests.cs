using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
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
        container.AddUltimaDataLoader<RegionsLoader, RegionContent>(8);
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

        var regions = service.GetEntities<RegionContent>();
        Assert.Equal(371, regions.Count);
        var britain = Assert.Single(regions, region => region.Map == MapType.Trammel && region.Name == "Britain");
        Assert.True(britain.Guarded);
        Assert.False(britain.Housing);
        Assert.Equal(MusicType.Britain1, britain.Music);
        Assert.Equal(new Point3D(1495, 1629, 10), britain.GoLocation);
        Assert.True(britain.Contains(1495, 1629, 10));
        Assert.All(regions.Where(region => region.Map == MapType.Ilshenar), region => Assert.False(region.RecallIn));
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
