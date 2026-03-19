using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Session;

namespace Moongate.Server.Interfaces.Services.Items;

public interface IItemManipulationService
{
    Task<bool> HandleDropItemAsync(
        GameSession session,
        DropItemPacket packet,
        CancellationToken cancellationToken = default
    );

    Task<bool> HandleDropWearItemAsync(
        GameSession session,
        DropWearItemPacket packet,
        CancellationToken cancellationToken = default
    );

    Task<bool> HandlePickUpItemAsync(
        GameSession session,
        PickUpItemPacket packet,
        CancellationToken cancellationToken = default
    );
}
