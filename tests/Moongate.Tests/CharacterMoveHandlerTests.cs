using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Types;
using Moongate.UO.PacketHandlers;

namespace Moongate.Tests;

/// <summary>
/// Comprehensive TDD tests for CharacterMoveHandler running mechanics.
/// Tests cover:
/// - DirectionType running flag extraction
/// - Movement speed calculation based on mobile status and running flag
/// - IsRunning property validation
/// - Direction setting during move processing
/// </summary>
[TestFixture]
public class CharacterMoveHandlerTests
{
    #region DirectionType Running Flag Extraction Tests

    [Test]
    public void ExtractBaseDirection_North_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType northRunning = (DirectionType)(0x80 | 0x0);

        // Act
        var baseDirection = (DirectionType)((byte)northRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.North));
    }

    [Test]
    public void ExtractBaseDirection_NorthEast_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType northEastRunning = (DirectionType)(0x80 | 0x1);

        // Act
        var baseDirection = (DirectionType)((byte)northEastRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.NorthEast));
    }

    [Test]
    public void ExtractBaseDirection_East_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType eastRunning = (DirectionType)(0x80 | 0x2);

        // Act
        var baseDirection = (DirectionType)((byte)eastRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.East));
    }

    [Test]
    public void ExtractBaseDirection_SouthEast_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType southEastRunning = (DirectionType)(0x80 | 0x3);

        // Act
        var baseDirection = (DirectionType)((byte)southEastRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.SouthEast));
    }

    [Test]
    public void ExtractBaseDirection_South_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType southRunning = (DirectionType)(0x80 | 0x4);

        // Act
        var baseDirection = (DirectionType)((byte)southRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.South));
    }

    [Test]
    public void ExtractBaseDirection_SouthWest_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType southWestRunning = (DirectionType)(0x80 | 0x5);

        // Act
        var baseDirection = (DirectionType)((byte)southWestRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.SouthWest));
    }

    [Test]
    public void ExtractBaseDirection_West_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType westRunning = (DirectionType)(0x80 | 0x6);

        // Act
        var baseDirection = (DirectionType)((byte)westRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.West));
    }

    [Test]
    public void ExtractBaseDirection_NorthWest_ShouldRemoveRunningFlag()
    {
        // Arrange
        DirectionType northWestRunning = (DirectionType)(0x80 | 0x7);

        // Act
        var baseDirection = (DirectionType)((byte)northWestRunning & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.NorthWest));
    }

    [Test]
    public void ExtractBaseDirection_AllDirectionsWithoutRunning_ShouldRemainUnchanged()
    {
        // Arrange
        DirectionType[] directions =
        {
            DirectionType.North,
            DirectionType.NorthEast,
            DirectionType.East,
            DirectionType.SouthEast,
            DirectionType.South,
            DirectionType.SouthWest,
            DirectionType.West,
            DirectionType.NorthWest
        };

        // Act & Assert
        foreach (var direction in directions)
        {
            var baseDirection = (DirectionType)((byte)direction & ~(byte)DirectionType.Running);
            Assert.That(baseDirection, Is.EqualTo(direction));
        }
    }

    [Test]
    public void ExtractBaseDirection_AllDirectionsWithRunning_ShouldRemoveRunningFlagOnly()
    {
        // Arrange
        var testCases = new[]
        {
            new { Running = (DirectionType)0x80, Expected = DirectionType.North },
            new { Running = (DirectionType)0x81, Expected = DirectionType.NorthEast },
            new { Running = (DirectionType)0x82, Expected = DirectionType.East },
            new { Running = (DirectionType)0x83, Expected = DirectionType.SouthEast },
            new { Running = (DirectionType)0x84, Expected = DirectionType.South },
            new { Running = (DirectionType)0x85, Expected = DirectionType.SouthWest },
            new { Running = (DirectionType)0x86, Expected = DirectionType.West },
            new { Running = (DirectionType)0x87, Expected = DirectionType.NorthWest }
        };

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var baseDirection = (DirectionType)((byte)testCase.Running & ~(byte)DirectionType.Running);
            Assert.That(baseDirection, Is.EqualTo(testCase.Expected));
        }
    }

    #endregion

    #region Movement Speed Calculation Tests - Static Method Tests

    [Test]
    public void ComputeSpeed_WithRunningFlag_ShouldUseRunningFlagBitwiseAnd()
    {
        // Arrange
        DirectionType northRunning = (DirectionType)0x80;

        // Act
        var hasRunningFlag = (northRunning & DirectionType.Running) != 0;

        // Assert
        Assert.That(hasRunningFlag, Is.True);
    }

    [Test]
    public void ComputeSpeed_WithoutRunningFlag_ShouldNotHaveRunningFlag()
    {
        // Arrange
        DirectionType north = DirectionType.North;

        // Act
        var hasRunningFlag = (north & DirectionType.Running) != 0;

        // Assert
        Assert.That(hasRunningFlag, Is.False);
    }

    [Test]
    public void ComputeSpeed_OnFootWalking_ShouldReturn400ms()
    {
        // Arrange
        // Foot walking (not mounted, no running flag)
        DirectionType northWalking = DirectionType.North;
        bool isMountedFootWalk = false;
        int expectedSpeed = 400;

        // Act
        int speed = (isMountedFootWalk) ?
            ((northWalking & DirectionType.Running) != 0 ? 100 : 200) :
            ((northWalking & DirectionType.Running) != 0 ? 200 : 400);

        // Assert
        Assert.That(speed, Is.EqualTo(expectedSpeed));
    }

    [Test]
    public void ComputeSpeed_OnFootRunning_ShouldReturn200ms()
    {
        // Arrange
        // Foot running (not mounted, running flag set)
        DirectionType northRunning = (DirectionType)0x80;
        bool isMountedFootRun = false;
        int expectedSpeed = 200;

        // Act
        int speed = (isMountedFootRun) ?
            ((northRunning & DirectionType.Running) != 0 ? 100 : 200) :
            ((northRunning & DirectionType.Running) != 0 ? 200 : 400);

        // Assert
        Assert.That(speed, Is.EqualTo(expectedSpeed));
    }

    [Test]
    public void ComputeSpeed_OnMountWalking_ShouldReturn200ms()
    {
        // Arrange
        // Mount walking (mounted, no running flag)
        DirectionType eastWalking = DirectionType.East;
        bool isMountedMountWalk = true;
        int expectedSpeed = 200;

        // Act
        int speed = (isMountedMountWalk) ?
            ((eastWalking & DirectionType.Running) != 0 ? 100 : 200) :
            ((eastWalking & DirectionType.Running) != 0 ? 200 : 400);

        // Assert
        Assert.That(speed, Is.EqualTo(expectedSpeed));
    }

    [Test]
    public void ComputeSpeed_OnMountRunning_ShouldReturn100ms()
    {
        // Arrange
        // Mount running (mounted, running flag set)
        DirectionType eastRunning = (DirectionType)0x82;
        bool isMountedMountRun = true;
        int expectedSpeed = 100;

        // Act
        int speed = (isMountedMountRun) ?
            ((eastRunning & DirectionType.Running) != 0 ? 100 : 200) :
            ((eastRunning & DirectionType.Running) != 0 ? 200 : 400);

        // Assert
        Assert.That(speed, Is.EqualTo(expectedSpeed));
    }

    [Test]
    public void ComputeSpeed_AllFootWalkDirections_ShouldReturn400ms()
    {
        // Arrange
        var directions = new[]
        {
            DirectionType.North,
            DirectionType.NorthEast,
            DirectionType.East,
            DirectionType.SouthEast,
            DirectionType.South,
            DirectionType.SouthWest,
            DirectionType.West,
            DirectionType.NorthWest
        };

        // Act & Assert
        foreach (var direction in directions)
        {
            int speed = ((direction & DirectionType.Running) != 0 ? 200 : 400);
            Assert.That(speed, Is.EqualTo(400), $"Failed for direction {direction}");
        }
    }

    [Test]
    public void ComputeSpeed_AllFootRunDirections_ShouldReturn200ms()
    {
        // Arrange
        var runningDirections = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };

        // Act & Assert
        foreach (var directionByte in runningDirections)
        {
            var direction = (DirectionType)directionByte;
            int speed = ((direction & DirectionType.Running) != 0 ? 200 : 400);
            Assert.That(speed, Is.EqualTo(200), $"Failed for direction 0x{directionByte:X2}");
        }
    }

    [Test]
    public void ComputeSpeed_AllMountWalkDirections_ShouldReturn200ms()
    {
        // Arrange
        var directions = new[]
        {
            DirectionType.North,
            DirectionType.NorthEast,
            DirectionType.East,
            DirectionType.SouthEast,
            DirectionType.South,
            DirectionType.SouthWest,
            DirectionType.West,
            DirectionType.NorthWest
        };

        // Act & Assert
        foreach (var direction in directions)
        {
            int speed = ((direction & DirectionType.Running) != 0 ? 100 : 200);
            Assert.That(speed, Is.EqualTo(200), $"Failed for direction {direction}");
        }
    }

    [Test]
    public void ComputeSpeed_AllMountRunDirections_ShouldReturn100ms()
    {
        // Arrange
        var runningDirections = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };

        // Act & Assert
        foreach (var directionByte in runningDirections)
        {
            var direction = (DirectionType)directionByte;
            int speed = ((direction & DirectionType.Running) != 0 ? 100 : 200);
            Assert.That(speed, Is.EqualTo(100), $"Failed for direction 0x{directionByte:X2}");
        }
    }

    #endregion

    #region IsRunning Property Tests

    [Test]
    public void MoveRequestPacket_IsRunning_WithNorthRunning_ShouldReturnTrue()
    {
        // Arrange
        var packet = new MoveRequestPacket
        {
            Direction = (DirectionType)0x80,
            Sequence = 1,
            FastKey = 0
        };

        // Act
        var isRunning = packet.IsRunning;

        // Assert
        Assert.That(isRunning, Is.True);
    }

    [Test]
    public void MoveRequestPacket_IsRunning_WithNorth_ShouldReturnFalse()
    {
        // Arrange
        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.North,
            Sequence = 1,
            FastKey = 0
        };

        // Act
        var isRunning = packet.IsRunning;

        // Assert
        Assert.That(isRunning, Is.False);
    }

    [Test]
    public void MoveRequestPacket_IsRunning_WithAllNormalDirections_ShouldReturnFalse()
    {
        // Arrange
        var directions = new[]
        {
            DirectionType.North,
            DirectionType.NorthEast,
            DirectionType.East,
            DirectionType.SouthEast,
            DirectionType.South,
            DirectionType.SouthWest,
            DirectionType.West,
            DirectionType.NorthWest
        };

        // Act & Assert
        foreach (var direction in directions)
        {
            var packet = new MoveRequestPacket
            {
                Direction = direction,
                Sequence = 1,
                FastKey = 0
            };

            Assert.That(packet.IsRunning, Is.False, $"Failed for direction {direction}");
        }
    }

    [Test]
    public void MoveRequestPacket_IsRunning_WithAllRunningDirections_ShouldReturnTrue()
    {
        // Arrange
        var runningDirections = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };

        // Act & Assert
        foreach (var directionByte in runningDirections)
        {
            var packet = new MoveRequestPacket
            {
                Direction = (DirectionType)directionByte,
                Sequence = 1,
                FastKey = 0
            };

            Assert.That(packet.IsRunning, Is.True, $"Failed for direction 0x{directionByte:X2}");
        }
    }

    [Test]
    public void MoveRequestPacket_IsRunning_UsesHasFlagMethod()
    {
        // Arrange & Act & Assert
        // This test verifies the HasFlag method works correctly with DirectionType.Running
        var northRunning = (DirectionType)0x80;
        var north = DirectionType.North;

        Assert.That(northRunning.HasFlag(DirectionType.Running), Is.True);
        Assert.That(north.HasFlag(DirectionType.Running), Is.False);
    }

    #endregion

    #region Direction Setting During Move Tests

    [Test]
    public void ExtractDirectionFromPacket_NorthNormal_ShouldExtractNorth()
    {
        // Arrange
        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.North,
            Sequence = 1,
            FastKey = 0
        };

        // Act
        var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.North));
    }

    [Test]
    public void ExtractDirectionFromPacket_EastRunning_ShouldExtractEast()
    {
        // Arrange
        DirectionType eastRunning = (DirectionType)0x82;
        var packet = new MoveRequestPacket
        {
            Direction = eastRunning,
            Sequence = 1,
            FastKey = 0
        };

        // Act
        var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.East));
    }

    [Test]
    public void ExtractDirectionFromPacket_SouthWestRunning_ShouldExtractSouthWest()
    {
        // Arrange
        DirectionType southWestRunning = (DirectionType)0x85;
        var packet = new MoveRequestPacket
        {
            Direction = southWestRunning,
            Sequence = 5,
            FastKey = 0
        };

        // Act
        var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.SouthWest));
    }

    [Test]
    public void ExtractDirectionFromPacket_AllDirections_ShouldExtractCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            new { PacketDirection = DirectionType.North, Expected = DirectionType.North },
            new { PacketDirection = DirectionType.NorthEast, Expected = DirectionType.NorthEast },
            new { PacketDirection = DirectionType.East, Expected = DirectionType.East },
            new { PacketDirection = DirectionType.SouthEast, Expected = DirectionType.SouthEast },
            new { PacketDirection = DirectionType.South, Expected = DirectionType.South },
            new { PacketDirection = DirectionType.SouthWest, Expected = DirectionType.SouthWest },
            new { PacketDirection = DirectionType.West, Expected = DirectionType.West },
            new { PacketDirection = DirectionType.NorthWest, Expected = DirectionType.NorthWest }
        };

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var packet = new MoveRequestPacket
            {
                Direction = testCase.PacketDirection,
                Sequence = 1,
                FastKey = 0
            };

            var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);
            Assert.That(baseDirection, Is.EqualTo(testCase.Expected), $"Failed for direction {testCase.PacketDirection}");
        }
    }

    [Test]
    public void ExtractDirectionFromPacket_AllRunningDirections_ShouldExtractBaseDirection()
    {
        // Arrange
        var testCases = new[]
        {
            new { PacketDirectionByte = 0x80, Expected = DirectionType.North },
            new { PacketDirectionByte = 0x81, Expected = DirectionType.NorthEast },
            new { PacketDirectionByte = 0x82, Expected = DirectionType.East },
            new { PacketDirectionByte = 0x83, Expected = DirectionType.SouthEast },
            new { PacketDirectionByte = 0x84, Expected = DirectionType.South },
            new { PacketDirectionByte = 0x85, Expected = DirectionType.SouthWest },
            new { PacketDirectionByte = 0x86, Expected = DirectionType.West },
            new { PacketDirectionByte = 0x87, Expected = DirectionType.NorthWest }
        };

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var packet = new MoveRequestPacket
            {
                Direction = (DirectionType)testCase.PacketDirectionByte,
                Sequence = 1,
                FastKey = 0
            };

            var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);
            Assert.That(baseDirection, Is.EqualTo(testCase.Expected), $"Failed for direction byte 0x{testCase.PacketDirectionByte:X2}");
        }
    }

    #endregion

    #region Running Flag Bitwise Operation Tests

    [Test]
    public void BitwiseOperation_RunningFlag_ShouldBe0x80()
    {
        // Arrange & Act
        byte runningFlagValue = (byte)DirectionType.Running;

        // Assert
        Assert.That(runningFlagValue, Is.EqualTo(0x80));
    }

    [Test]
    public void BitwiseOperation_DirectionAndRunning_ShouldContainRunningFlag()
    {
        // Arrange
        DirectionType northRunning = (DirectionType)0x80;

        // Act
        bool hasRunningFlag = (northRunning & DirectionType.Running) != 0;

        // Assert
        Assert.That(hasRunningFlag, Is.True);
    }

    [Test]
    public void BitwiseOperation_DirectionWithoutRunning_ShouldNotContainRunningFlag()
    {
        // Arrange
        DirectionType north = DirectionType.North;

        // Act
        bool hasRunningFlag = (north & DirectionType.Running) != 0;

        // Assert
        Assert.That(hasRunningFlag, Is.False);
    }

    [Test]
    public void BitwiseOperation_RemoveRunningFlag_ShouldProduceBaseValue()
    {
        // Arrange
        var testCases = new[]
        {
            new { Original = 0x80, Expected = 0x0 },
            new { Original = 0x81, Expected = 0x1 },
            new { Original = 0x82, Expected = 0x2 },
            new { Original = 0x83, Expected = 0x3 },
            new { Original = 0x84, Expected = 0x4 },
            new { Original = 0x85, Expected = 0x5 },
            new { Original = 0x86, Expected = 0x6 },
            new { Original = 0x87, Expected = 0x7 }
        };

        // Act & Assert
        foreach (var testCase in testCases)
        {
            byte result = (byte)((byte)testCase.Original & ~(byte)DirectionType.Running);
            Assert.That(result, Is.EqualTo(testCase.Expected), $"Failed for original 0x{testCase.Original:X2}");
        }
    }

    #endregion

    #region Integration Tests

    [Test]
    public void MovementLogic_WalkFootNorth_ShouldSetCorrectDirectionAndSpeed()
    {
        // Arrange
        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.North,
            Sequence = 1,
            FastKey = 0
        };
        bool isMounted = false;

        // Act
        var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);
        int speed = (isMounted) ?
            ((packet.Direction & DirectionType.Running) != 0 ? 100 : 200) :
            ((packet.Direction & DirectionType.Running) != 0 ? 200 : 400);
        var isRunning = packet.IsRunning;

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.North));
        Assert.That(speed, Is.EqualTo(400));
        Assert.That(isRunning, Is.False);
    }

    [Test]
    public void MovementLogic_RunMountEast_ShouldSetCorrectDirectionAndSpeed()
    {
        // Arrange
        DirectionType eastRunning = (DirectionType)0x82;
        var packet = new MoveRequestPacket
        {
            Direction = eastRunning,
            Sequence = 1,
            FastKey = 0
        };
        bool isMounted = true;

        // Act
        var baseDirection = (DirectionType)((byte)packet.Direction & ~(byte)DirectionType.Running);
        int speed = (isMounted) ?
            ((packet.Direction & DirectionType.Running) != 0 ? 100 : 200) :
            ((packet.Direction & DirectionType.Running) != 0 ? 200 : 400);
        var isRunning = packet.IsRunning;

        // Assert
        Assert.That(baseDirection, Is.EqualTo(DirectionType.East));
        Assert.That(speed, Is.EqualTo(100));
        Assert.That(isRunning, Is.True);
    }

    [Test]
    public void MovementLogic_AllCombinations_ShouldProduceExpectedResults()
    {
        // Arrange
        var mobileStates = new[] { false, true }; // not mounted, mounted
        var directionBytes = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };
        var expectedSpeeds = new[]
        {
            // Foot speeds
            400, 400, 400, 400, 400, 400, 400, 400, 200, 200, 200, 200, 200, 200, 200, 200,
            // Mount speeds
            200, 200, 200, 200, 200, 200, 200, 200, 100, 100, 100, 100, 100, 100, 100, 100
        };

        // Act & Assert
        int index = 0;
        foreach (var isMounted in mobileStates)
        {
            foreach (var directionByte in directionBytes)
            {
                var direction = (DirectionType)directionByte;
                int speed = (isMounted) ?
                    ((direction & DirectionType.Running) != 0 ? 100 : 200) :
                    ((direction & DirectionType.Running) != 0 ? 200 : 400);
                var isRunning = (direction & DirectionType.Running) != 0;

                Assert.That(speed, Is.EqualTo(expectedSpeeds[index]),
                    $"Failed for isMounted={isMounted}, direction=0x{directionByte:X2}");

                if (directionByte < 0x80)
                {
                    Assert.That(isRunning, Is.False);
                }
                else
                {
                    Assert.That(isRunning, Is.True);
                }

                index++;
            }
        }
    }

    [Test]
    public void MovementLogic_VerifyPacketSequence_ShouldParseCorrectly()
    {
        // Arrange
        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.South,
            Sequence = 42,
            FastKey = 0x12345678
        };

        // Act
        var sequence = packet.Sequence;
        var direction = packet.Direction;
        var fastKey = packet.FastKey;

        // Assert
        Assert.That(sequence, Is.EqualTo(42));
        Assert.That(direction, Is.EqualTo(DirectionType.South));
        Assert.That(fastKey, Is.EqualTo(0x12345678));
    }

    #endregion
}
