using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Data;
using Moongate.Server.Interfaces;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Handlers;

/// <summary>Handles game server login (0x91): validates the handoff key and sends the character list.</summary>
public sealed class GameServerLoginHandler : IPacketHandler<GameServerLoginPacket>, IPacketHandlerRegistration
{
    private const byte CharacterSlots = 7;

    private readonly IPendingLoginStore _pendingLogins;
    private readonly IStartingCityService _cities;

    public GameServerLoginHandler(IPendingLoginStore pendingLogins, IStartingCityService cities)
    {
        _pendingLogins = pendingLogins;
        _cities = cities;
    }

    public void Handle(GameServerLoginPacket packet, in PacketContext context)
    {
        if (!_pendingLogins.TryTake(packet.AuthKey, out var pending))
        {
            context.Session.Send(new LoginDeniedPacket(LoginDeniedReasonType.CommunicationProblem));

            return;
        }

        context.Session.MarkAuthenticated(pending.Username);
        context.Session.Send(new CharacterListPacket(_cities.All, CharacterSlots, CharacterListFlagType.Modern));
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
