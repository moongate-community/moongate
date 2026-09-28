using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Packets.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     Receives the character the client chose to play (0x5D). Entering the world is not built yet: the choice is only
///     logged.
/// </summary>
public sealed class PlayCharacterPacketHandler : IPacketHandler<PlayCharacterPacket>
{
    private readonly ILogger _logger = Log.ForContext<PlayCharacterPacketHandler>();

    public void Handle(GameSession session, PlayCharacterPacket packet)
    {
        _logger.Information(
            "Session {SessionId}: account {AccountId} chose character {Name} at list position {Index}",
            session.SessionId,
            session.AccountId,
            packet.Name,
            packet.CharacterIndex
        );
    }
}
