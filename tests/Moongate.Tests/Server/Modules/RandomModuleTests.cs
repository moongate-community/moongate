using Moongate.Server.Modules;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Modules;

public sealed class RandomModuleTests
{
    private static readonly DirectionType[] AllowedDirections =
    [
        DirectionType.North,
        DirectionType.NorthEast,
        DirectionType.East,
        DirectionType.SouthEast,
        DirectionType.South,
        DirectionType.SouthWest,
        DirectionType.West,
        DirectionType.NorthWest
    ];

    [Test]
    public void Direction_ShouldReturnOnlyBaseDirections()
    {
        var module = new RandomModule();

        for (var i = 0; i < 100; i++)
        {
            var direction = module.Direction();
            Assert.That(AllowedDirections, Does.Contain(direction));
        }
    }

    [Test]
    public void Int_ShouldReturnInclusiveRange()
    {
        var module = new RandomModule();

        for (var i = 0; i < 200; i++)
        {
            var value = module.Int(1, 3);
            Assert.That(value, Is.InRange(1, 3));
        }
    }

    [Test]
    public void Int_WhenMinGreaterThanMax_ShouldSwapBounds()
    {
        var module = new RandomModule();

        for (var i = 0; i < 200; i++)
        {
            var value = module.Int(10, 7);
            Assert.That(value, Is.InRange(7, 10));
        }
    }
}
