using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Services.Movement;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Movement;

public sealed class AStarPathfindingServiceTests
{
    [Test]
    public void TryFindPath_WhenStraightLineIsWalkable_ShouldReturnDirectPath()
    {
        var movementValidationService = new AStarPathfindingTestMovementValidationService();
        var service = new AStarPathfindingService(movementValidationService);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x01u,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };

        var found = service.TryFindPath(mobile, new Point3D(103, 100, 0), out var path);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(path, Is.Not.Empty);
                Assert.That(path[0], Is.EqualTo(DirectionType.East));
            }
        );
    }

    [Test]
    public void TryFindPath_WhenDirectTileBlocked_ShouldFindAlternativeRoute()
    {
        var movementValidationService = new AStarPathfindingTestMovementValidationService();
        movementValidationService.BlockedTiles.Add((101, 100));
        var service = new AStarPathfindingService(movementValidationService);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x01u,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };

        var found = service.TryFindPath(mobile, new Point3D(102, 100, 0), out var path);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(path, Is.Not.Empty);
                Assert.That(path[0], Is.Not.EqualTo(DirectionType.East));
            }
        );
    }

    [Test]
    public void TryFindPath_WhenMaxVisitedNodesTooLow_ShouldReturnFalse()
    {
        var movementValidationService = new AStarPathfindingTestMovementValidationService();
        var service = new AStarPathfindingService(movementValidationService);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x01u,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };

        var found = service.TryFindPath(mobile, new Point3D(500, 500, 0), out _, maxVisitedNodes: 1);

        Assert.That(found, Is.False);
    }

    private sealed class AStarPathfindingTestMovementValidationService : IMovementValidationService
    {
        public HashSet<(int X, int Y)> BlockedTiles { get; } = [];

        public bool TryResolveMove(UOMobileEntity mobile, DirectionType direction, out Point3D newLocation)
        {
            newLocation = mobile.Location.Move(direction);

            if (BlockedTiles.Contains((newLocation.X, newLocation.Y)))
            {
                return false;
            }

            return true;
        }
    }
}
