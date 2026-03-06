using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.UO.Data.Persistence.Entities;

public sealed class UOItemEntityCustomLocationTests
{
    [Test]
    public void SetCustomLocation_ThenTryGetCustomLocation_ShouldRoundTrip()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40001000,
            ItemId = 0x0EED
        };

        var expected = new Point3D(1595, 2490, 20);

        item.SetCustomLocation("point_dest", expected);

        var found = item.TryGetCustomLocation("point_dest", out var actual);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(actual, Is.EqualTo(expected));
            }
        );
    }

    [Test]
    public void TryGetCustomLocation_WhenValueIsInvalid_ShouldReturnFalse()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40001001,
            ItemId = 0x0EED
        };

        item.SetCustomString("point_dest", "not-a-location");

        var found = item.TryGetCustomLocation("point_dest", out var actual);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.False);
                Assert.That(actual, Is.EqualTo(default(Point3D)));
            }
        );
    }
}
