using Moongate.Server.Services.World;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.World;

public class DoorGenerationFacingExtensionsTests
{
    [TestCase(DoorGenerationFacing.WestCW, DirectionType.West), TestCase(DoorGenerationFacing.EastCCW, DirectionType.East),
     TestCase(DoorGenerationFacing.SouthCW, DirectionType.South),
     TestCase(DoorGenerationFacing.NorthCCW, DirectionType.North)]
    public void ToDirectionType_ShouldMapKnownFacingValues(DoorGenerationFacing facing, DirectionType expected)
    {
        var direction = facing.ToDirectionType();

        Assert.That(direction, Is.EqualTo(expected));
    }

    [Test]
    public void ToDirectionType_ShouldThrow_WhenFacingIsUnknown()
        => Assert.That(
            () => ((DoorGenerationFacing)255).ToDirectionType(),
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );

    [TestCase(DoorGenerationFacing.WestCW, 0x0675, 0x0675), TestCase(DoorGenerationFacing.EastCCW, 0x0675, 0x0677),
     TestCase(DoorGenerationFacing.SouthCW, 0x0675, 0x067D), TestCase(DoorGenerationFacing.NorthCCW, 0x0675, 0x067F)]
    public void ToItemId_ShouldMapFacingUsingDoorStride(
        DoorGenerationFacing facing,
        int baseItemId,
        int expectedItemId
    )
    {
        var itemId = facing.ToItemId(baseItemId);

        Assert.That(itemId, Is.EqualTo(expectedItemId));
    }

    [Test]
    public void ToItemId_ShouldThrow_WhenFacingIsUnknown()
        => Assert.That(
            () => ((DoorGenerationFacing)255).ToItemId(0x0675),
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
}
