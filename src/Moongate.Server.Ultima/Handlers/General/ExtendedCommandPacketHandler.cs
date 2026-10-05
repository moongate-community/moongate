using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Dispatches the extended commands (0xBF) by subcommand: 0x10 asks for one object's tooltip, answered with 0xD6
///     when the character can see it; 0x1A sets the lock of one stat of the character. The others are recognised and
///     ignored for now.
/// </summary>
public sealed class ExtendedCommandPacketHandler : IPacketHandler<ExtendedCommandPacket>
{
    private const ushort QueryPropertiesSubcommand = 0x10;
    private const ushort StatLockSubcommand = 0x1A;
    private const int StatLockLength = 2;

    private readonly ILogger _logger = Log.ForContext<ExtendedCommandPacketHandler>();
    private readonly ITooltipService _tooltips;
    private readonly IPacketSendService _sender;
    private readonly IMobileService? _mobiles;
    private readonly IMobileStateService? _state;

    public ExtendedCommandPacketHandler(
        ITooltipService tooltips,
        IPacketSendService sender,
        IMobileService? mobiles = null,
        IMobileStateService? state = null
    )
    {
        _mobiles = mobiles;
        _state = state;
        _tooltips = tooltips;
        _sender = sender;
    }

    public void Handle(GameSession session, ExtendedCommandPacket packet)
    {
        if (packet.Subcommand == QueryPropertiesSubcommand && packet.Payload.Length >= 4)
        {
            var serial = new Serial(BinaryPrimitives.ReadUInt32BigEndian(packet.Payload));

            if (_tooltips.TryBuildFor(session.CharacterId, serial, out var list, session.AccountType))
            {
                _sender.TrySend(session.SessionId, new PropertyListPacket(serial, list));
            }

            return;
        }

        if (packet.Subcommand == StatLockSubcommand && packet.Payload.Length >= StatLockLength)
        {
            SetStatLock(session, packet.Payload[0], packet.Payload[1]);

            return;
        }

        _logger.Debug("Session {SessionId} sent extended command 0x{Subcommand:X2}, not handled yet", session.SessionId, packet.Subcommand);
    }

    // The status window's lock arrows: the stat is 0 strength, 1 dexterity, 2 intelligence, the lock as the client
    // counts it; as ModernUO, a lock past locked reads as up.
    private void SetStatLock(GameSession session, byte stat, byte lockValue)
    {
        if (_mobiles is null ||
            _state is null ||
            !session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var character) ||
            !_mobiles.IsInWorld(character.Id))
        {
            return;
        }

        _state.SetStatLock(
            character,
            (StatType)stat,
            lockValue > (byte)StatLockType.Locked ? StatLockType.Up : (StatLockType)lockValue
        );
    }
}
