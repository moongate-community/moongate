using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Weather;

namespace Moongate.Tests.Server.FileLoaders;

public class JsonFileLoadersNegativeTests
{
    [Test]
    public void ContainersDataLoader_WhenJsonIsInvalid_ShouldThrowJsonException()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var containersPath = Path.Combine(directories[DirectoryType.Data], "containers");
        Directory.CreateDirectory(containersPath);
        File.WriteAllText(Path.Combine(containersPath, "broken.json"), "{ not-a-json }");

        var loader = new ContainersDataLoader(directories);

        Assert.ThrowsAsync<JsonException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void RegionDataLoader_WhenRegionsDirectoryDoesNotExist_ShouldThrowDirectoryNotFoundException()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var loader = new RegionDataLoader(directories, new RegionDataLoaderTestSpatialWorldService());

        Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void WeatherDataLoader_WhenWeatherDirectoryDoesNotExist_ShouldThrowDirectoryNotFoundException()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var loader = new WeatherDataLoader(directories, new NullWeatherService());

        Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await loader.LoadAsync());
    }

    private sealed class NullWeatherService : IWeatherService
    {
        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public void SetWeatherTypes(List<JsonWeather> weatherTypes) { }

        public WeatherSnapshot GenerateSnapshot(JsonWeather weather, Random? random = null)
            => default;

        public int ComputeGlobalLightLevel(DateTime? utcNow = null)
            => 0;

        public int ComputeGlobalLightLevel(int mapId, Moongate.UO.Data.Geometry.Point3D location, DateTime? utcNow = null)
            => 0;

        public void SetGlobalLightOverride(int? lightLevel, bool applyImmediately = true) { }
    }
}
