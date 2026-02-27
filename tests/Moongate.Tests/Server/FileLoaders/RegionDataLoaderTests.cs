using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public class RegionDataLoaderTests
{
    [Test]
    public async Task LoadAsync_ShouldPopulateSpatialRegions()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var regionsPath = Path.Combine(directories[DirectoryType.Data], "regions");
        Directory.CreateDirectory(regionsPath);

        const string json = """
                            [
                              {
                                "$type": "TownRegion",
                                "Map": "Felucca",
                                "Name": "Test Region",
                                "Area": [
                                  { "x1": 100, "y1": 100, "x2": 120, "y2": 120 }
                                ]
                              }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(regionsPath, "regions.json"), json);

        var spatialWorldService = new RegionDataLoaderTestSpatialWorldService();
        var loader = new RegionDataLoader(directories, spatialWorldService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(spatialWorldService.AddedRegions, Has.Count.EqualTo(1));
                Assert.That(spatialWorldService.AddedRegions[0].Name, Is.EqualTo("Test Region"));
                Assert.That(spatialWorldService.AddedRegions[0].MapId, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WithModernUoArrayFormat_ShouldConvertRegionsAndMusic()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var regionsPath = Path.Combine(directories[DirectoryType.Data], "regions");
        Directory.CreateDirectory(regionsPath);

        const string modernUoJson = """
                                    [
                                      {
                                        "$type": "TownRegion",
                                        "Map": "Felucca",
                                        "Name": "Britain",
                                        "Area": [
                                          { "x1": 1416, "y1": 1498, "x2": 1740, "y2": 1777 }
                                        ],
                                        "Music": "Britain1"
                                      }
                                    ]
                                    """;

        await File.WriteAllTextAsync(Path.Combine(regionsPath, "regions.json"), modernUoJson);

        var spatialWorldService = new RegionDataLoaderTestSpatialWorldService();
        var loader = new RegionDataLoader(directories, spatialWorldService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(spatialWorldService.AddedRegions, Has.Count.EqualTo(1));
                Assert.That(spatialWorldService.AddedRegions[0].Name, Is.EqualTo("Britain"));
                Assert.That(spatialWorldService.AddedRegions[0].Area, Has.Count.EqualTo(1));
                Assert.That(spatialWorldService.AddedRegions[0].Music, Is.EqualTo(MusicName.Britain1));
                Assert.That(spatialWorldService.AddedRegions[0].MapId, Is.EqualTo(0));
            }
        );
    }
}
