using Moongate.Server.Services.World;
using Moongate.UO.Data.Geometry;

namespace Moongate.Tests.Server.Services.World;

public class DoorDataServiceTests
{
    [Test]
    public void SetEntries_ShouldBuildToggleDefinitionsUsingDoorsTxtOrder()
    {
        var service = new DoorDataService();
        service.SetEntries(
            [
                new(
                    0,
                    0x0679,
                    0x067B,
                    0x0675,
                    0x0677,
                    0x067D,
                    0x067F,
                    0x0681,
                    0x0683,
                    0,
                    "Metal Door"
                )
            ]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(service.TryGetToggleDefinition(0x0675, out var west), Is.True);
                Assert.That(west.IsClosed, Is.True);
                Assert.That(west.NextItemId, Is.EqualTo(0x0676));
                Assert.That(west.Offset, Is.EqualTo(new Point3D(-1, 1, 0)));

                Assert.That(service.TryGetToggleDefinition(0x0676, out var westOpen), Is.True);
                Assert.That(westOpen.IsClosed, Is.False);
                Assert.That(westOpen.NextItemId, Is.EqualTo(0x0675));

                Assert.That(service.TryGetToggleDefinition(0x0674, out var legacy), Is.True);
                Assert.That(legacy.NextItemId, Is.EqualTo(0x0676));
                Assert.That(legacy.Offset, Is.EqualTo(new Point3D(-1, 1, 0)));
            }
        );
    }
}
