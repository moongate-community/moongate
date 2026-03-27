using Moongate.Server.Modules;
using Moongate.Tests.Server.Modules.Support;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Modules;

public class MobileMovementModuleTests
{
    [Test]
    public void Teleport_WhenCharacterExists_ShouldUpdateMapAndLocation()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x220,
            Name = "Traveler",
            MapId = 1,
            Location = new(100, 200, 5)
        };
        var module = new MobileMovementModule(
            new MobileModuleTestSpeechService(),
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService()
        );

        var teleported = module.Teleport(mobile, 0, 1496, 1628, 20);

        Assert.Multiple(
            () =>
            {
                Assert.That(teleported, Is.True);
                Assert.That(mobile.MapId, Is.EqualTo(0));
                Assert.That(mobile.Location, Is.EqualTo(new Point3D(1496, 1628, 20)));
            }
        );
    }
}
