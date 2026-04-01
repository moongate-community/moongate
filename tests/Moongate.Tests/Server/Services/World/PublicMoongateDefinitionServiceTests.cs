using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.World;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;

namespace Moongate.Tests.Server.Services.World;

public sealed class PublicMoongateDefinitionServiceTests
{
    [Test]
    public void Load_WhenDataFileIsValid_ShouldReturnTypedGroups()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var moongatesDirectory = Path.Combine(directories[DirectoryType.Scripts], "moongates");
        Directory.CreateDirectory(moongatesDirectory);

        File.WriteAllText(
            Path.Combine(moongatesDirectory, "data.lua"),
            """
            local data = {}

            function data.load()
              return {
                {
                  id = "felucca",
                  name = "Felucca",
                  destinations = {
                    { id = "moonglow", name = "Moonglow", map = "felucca", x = 4467, y = 1283, z = 5 }
                  }
                },
                {
                  id = "ilshenar",
                  name = "Ilshenar",
                  destinations = {
                    { id = "chaos", name = "Chaos", map = "ilshenar", x = 1721, y = 218, z = 96 }
                  }
                }
              }
            end

            return data
            """
        );

        var service = new PublicMoongateDefinitionService(directories);

        var groups = service.Load();

        Assert.Multiple(
            () =>
            {
                Assert.That(groups, Has.Count.EqualTo(2));
                Assert.That(groups[0].Id, Is.EqualTo("felucca"));
                Assert.That(groups[0].Destinations, Has.Count.EqualTo(1));
                Assert.That(groups[0].Destinations[0].MapId, Is.EqualTo(0));
                Assert.That(groups[0].Destinations[0].Location, Is.EqualTo(new Point3D(4467, 1283, 5)));
                Assert.That(groups[1].Id, Is.EqualTo("ilshenar"));
                Assert.That(groups[1].Destinations[0].MapId, Is.EqualTo(2));
            }
        );
    }

    [Test]
    public void Load_WhenDestinationMapIsUnknown_ShouldThrow()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var moongatesDirectory = Path.Combine(directories[DirectoryType.Scripts], "moongates");
        Directory.CreateDirectory(moongatesDirectory);

        File.WriteAllText(
            Path.Combine(moongatesDirectory, "data.lua"),
            """
            local data = {}

            function data.load()
              return {
                {
                  id = "broken",
                  name = "Broken",
                  destinations = {
                    { id = "oops", name = "Oops", map = "unknown_map", x = 10, y = 20, z = 0 }
                  }
                }
              }
            end

            return data
            """
        );

        var service = new PublicMoongateDefinitionService(directories);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Load());
        Assert.That(exception!.Message, Does.Contain("unknown_map"));
    }
}
