using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;

namespace Moongate.UO.Extensions;

public static class MobileExtensions
{
    public static void SendPackets(this UOMobileEntity mobile, params IUoNetworkPacket[] packets)
    {
        if (mobile.IsPlayer)
        {
            var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();

            var session = gameSessionService.QuerySessionFirstOrDefault(s => s.Mobile?.Id == mobile.Id);

            if (session == null)
            {
                throw new InvalidOperationException($"No active session found for mobile {mobile.Id}.");
            }

            session.SendPackets(packets);
        }
    }
}
