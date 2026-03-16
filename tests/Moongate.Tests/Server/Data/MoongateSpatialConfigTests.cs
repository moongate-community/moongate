using Moongate.Server.Data.Config;

namespace Moongate.Tests.Server.Data;

public sealed class MoongateSpatialConfigTests
{
    [Test]
    public void Constructor_ShouldDefaultLazySectorEntityLoadRadiusToOne()
    {
        var config = new MoongateSpatialConfig();

        Assert.That(config.LazySectorEntityLoadRadius, Is.EqualTo(1));
    }

    [Test]
    public void Constructor_ShouldDefaultSectorEnterSyncRadiusToTwo()
    {
        var config = new MoongateSpatialConfig();

        Assert.That(config.SectorEnterSyncRadius, Is.EqualTo(2));
    }
}
