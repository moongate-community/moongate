using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server;

public class RegionDataLoaderTests
{
    [Test]
    public async Task LoadAsync_ShouldPopulateSpatialRegionsAndMusics()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var regionsPath = Path.Combine(directories[DirectoryType.Data], "regions");
        Directory.CreateDirectory(regionsPath);

        const string json = """
                            {
                              "header": {
                                "title": "test",
                                "repository": "repo",
                                "lastUpdate": "today",
                                "script": "regions.dfn",
                                "description": "test"
                              },
                              "regions": [
                                {
                                  "id": 1,
                                  "name": "Test Region",
                                  "musiclist": 10,
                                  "coordinates": [
                                    { "x1": 100, "y1": 100, "x2": 120, "y2": 120 }
                                  ]
                                }
                              ],
                              "musicLists": [
                                { "id": 10, "name": "test_music", "music": 33 }
                              ]
                            }
                            """;
        await File.WriteAllTextAsync(Path.Combine(regionsPath, "regions.json"), json);

        var spatialWorldService = new RegionDataLoaderTestSpatialWorldService();
        var loader = new RegionDataLoader(directories, spatialWorldService);

        await loader.LoadAsync();

        Assert.That(spatialWorldService.AddedRegions, Has.Count.EqualTo(1));
        Assert.That(spatialWorldService.AddedMusics, Has.Count.EqualTo(1));
    }
}
