using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Dispatches the extended commands (0xBF) by subcommand: 0x10 asks for one object's tooltip, answered with 0xD6
///     when the character can see it. The others are recognised and ignored for now.
/// </summary>
public sealed class ExtendedCommandPacketHandler : IPacketHandler<ExtendedCommandPacket>
{
    private const ushort QueryPropertiesSubcommand = 0x10;

    private readonly ILogger _logger = Log.ForContext<ExtendedCommandPacketHandler>();
    private readonly ITooltipService _tooltips;
    private readonly IPacketSendService _sender;

    public ExtendedCommandPacketHandler(ITooltipService tooltips, IPacketSendService sender)
    {
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

        _logger.Debug("Session {SessionId} sent extended command 0x{Subcommand:X2}, not handled yet", session.SessionId, packet.Subcommand);
    }
}
