using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.HuePicking;

/// <summary>
///     Hands the client's hue picker answer (0x95) to the player's open picker. An answer for another picker, or with
///     none open, is ignored: a client cannot recolour anything by sending the packet on its own.
/// </summary>
public sealed class HuePickerResponsePacketHandler : IPacketHandler<HuePickerResponsePacket>
{
    private readonly ILogger _logger = Log.ForContext<HuePickerResponsePacketHandler>();
    private readonly IHuePickerService _pickers;

    public HuePickerResponsePacketHandler(IHuePickerService pickers)
    {
        _pickers = pickers;
    }

    public void Handle(GameSession session, HuePickerResponsePacket packet)
    {
        if (!_pickers.TryComplete(session, packet.PickerId, packet.Hue))
        {
            _logger.Debug(
                "Session {SessionId} answered hue picker {PickerId}, which is not open",
                session.SessionId,
                packet.PickerId
            );
        }
    }
}
