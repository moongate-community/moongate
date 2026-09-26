using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class WeatherLoaderTests
{
    private const string Snowy = """
                                 [[weather]]
                                 name = "snowy"
                                 rain_chance = 0
                                 snow_chance = 90
                                 storm_chance = 40
                                 snow_threshold = 5
                                 min_temperature = 4
                                 max_temperature = 10
                                 cold_chance = 90
                                 cold_temperature = 1
                                 heat_chance = 0
                                 heat_temperature = 0
                                 rain_temperature_drop = 0
                                 storm_temperature_drop = 10

                                 """;

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEveryField()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/weather.toml", Snowy);

        var snowy = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal("snowy", snowy.Name);
        Assert.Equal((0, 90, 40), (snowy.RainChance, snowy.SnowChance, snowy.StormChance));
        Assert.Equal(5, snowy.SnowThreshold);
        Assert.Equal((4, 10), (snowy.MinTemperature, snowy.MaxTemperature));
        Assert.Equal((90, 1), (snowy.ColdChance, snowy.ColdTemperature));
        Assert.Equal((0, 10), (snowy.RainTemperatureDrop, snowy.StormTemperatureDrop));
    }

    [Theory,
     InlineData("snow_chance = 90", "snow_chance = 101"),
     InlineData("cold_chance = 90", "cold_chance = -1"),
     InlineData("min_temperature = 4", "min_temperature = 11"),
     InlineData("name = \"snowy\"", "name = \"\"")]
    public async Task LoadDataAsync_InvalidProfile_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/weather.toml", Snowy.Replace(from, to));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_NameUsedTwiceOrNoProfiles_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/weather.toml", Snowy + Snowy);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        root.CreateFile("data/weather.toml", "# nothing\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static WeatherLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
