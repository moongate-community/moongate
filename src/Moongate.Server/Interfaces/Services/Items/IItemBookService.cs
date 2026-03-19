using Moongate.Network.Packets.Incoming.Books;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Items;

public interface IItemBookService
{
    Task<bool> HandleBookHeaderAsync(
        GameSession session,
        BookHeaderNewPacket packet,
        CancellationToken cancellationToken = default
    );

    Task<bool> HandleBookPagesAsync(
        GameSession session,
        BookPagesPacket packet,
        CancellationToken cancellationToken = default
    );

    Task<bool> TryEnqueueBookAsync(
        GameSession session,
        UOItemEntity item,
        CancellationToken cancellationToken = default
    );
}
