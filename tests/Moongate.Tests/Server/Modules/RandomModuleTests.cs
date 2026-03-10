using Moongate.Server.Modules;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

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

    [Test]
    public void Element_WhenArrayTableHasValues_ShouldReturnOneOfValues()
    {
        var module = new RandomModule();
        var table = new Table(new Script());
        table[1] = "a";
        table[2] = "b";
        table[3] = "c";

        for (var i = 0; i < 50; i++)
        {
            var value = module.Element(table);
            Assert.That(value.Type, Is.EqualTo(DataType.String));
            Assert.That(new[] { "a", "b", "c" }, Does.Contain(value.String));
        }
    }

    [Test]
    public void Element_WhenTableIsEmpty_ShouldReturnNil()
    {
        var module = new RandomModule();
        var table = new Table(new Script());

        var value = module.Element(table);

        Assert.That(value.IsNil(), Is.True);
    }
}
