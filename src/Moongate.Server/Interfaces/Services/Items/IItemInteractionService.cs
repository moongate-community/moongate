using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Session;

namespace Moongate.Server.Interfaces.Services.Items;

public interface IItemInteractionService
{
    Task<bool> HandleDoubleClickAsync(
        GameSession session,
        DoubleClickPacket packet,
        CancellationToken cancellationToken = default
    );

    Task<bool> HandleSingleClickAsync(
        GameSession session,
        SingleClickPacket packet,
        CancellationToken cancellationToken = default
    );
}
