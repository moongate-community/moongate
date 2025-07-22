using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Contexts;

public class AiContext : IDisposable
{
    protected UOMobileEntity MobileEntity;
    private static readonly DirectionType[] directions =
    {
        DirectionType.North, DirectionType.Left, DirectionType.East, DirectionType.Right,
        DirectionType.South, DirectionType.West
    };

    public void InitializeContext(UOMobileEntity mobile)
    {
        MobileEntity = mobile;
    }

    public void Say(string message)
    {
        MobileEntity.Speech(ChatMessageType.Regular, 1168, message, 0, 3);
    }

    public void Move(DirectionType direction)
    {
        if (MobileEntity == null)
        {
            throw new InvalidOperationException("MobileEntity is not initialized.");
        }

        var newLocation = MobileEntity.Location + direction;

        var landTile = MobileEntity.Map.GetLandTile(newLocation.X, newLocation.Y);

        newLocation = new Point3D(newLocation.X, newLocation.Y, landTile.Z);

        MoongateContext.Container.Resolve<IMobileService>().MoveMobile(MobileEntity, newLocation);
    }

    public DirectionType RandomDirection()
    {
        return directions[Random.Shared.Next(directions.Length)];
    }

    public void Dispose()
    {
    }
}
