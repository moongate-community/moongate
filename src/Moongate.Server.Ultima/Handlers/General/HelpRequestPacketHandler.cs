using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Opens the help gump of the character whose player pressed the Help button of the paperdoll.
/// </summary>
public sealed class HelpRequestPacketHandler : IPacketHandler<HelpRequestPacket>
{
    private const string GumpId = "help_menu";

    private readonly IMobileService _mobiles;
    private readonly GumpModule? _gumps;

    public HelpRequestPacketHandler(IMobileService mobiles, GumpModule? gumps = null)
    {
        _mobiles = mobiles;
        _gumps = gumps;
    }

    public void Handle(GameSession session, HelpRequestPacket packet)
    {
        if (session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out var character))
        {
            _gumps?.Open(character.Id.Value, GumpId);
        }
    }
}
