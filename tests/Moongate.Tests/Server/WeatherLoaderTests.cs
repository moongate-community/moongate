using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class WeatherLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-weather-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAllTypes()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var weather = new WeatherService();
        var loader = new WeatherLoader(weather, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "weather.yaml")));
            Assert.Equal(11, weather.Count);
            var desert = weather.All.Single(w => w.Name == "Desert");
            Assert.True(desert.RainIntensity.Max >= desert.RainIntensity.Min);
            Assert.Equal("Intense Rain", weather.GetById(9)!.Name);
            Assert.Equal("Intense Snow", weather.GetById(10)!.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPresent_LoadsExistingWithoutReseeding()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var dataDir = directories.RegisterDirectory("data");
        File.WriteAllText(
            Path.Combine(dataDir, "weather.yaml"),
            "- Id: 5\n  Name: Custom\n  RainIntensity:\n    Min: 1\n    Max: 3\n"
        );
        var weather = new WeatherService();
        var loader = new WeatherLoader(weather, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, weather.Count);
            Assert.Equal(3, weather.GetById(5)!.RainIntensity.Max);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
