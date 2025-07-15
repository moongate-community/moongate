using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;

namespace Moongate.UO.PacketHandlers;

public class PlayerStatusHandler : IGamePacketHandler
{
    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is GetPlayerStatusPacket getPlayerStatusPacket)
        {
            await HandleGetPlayerStatusAsync(session, getPlayerStatusPacket);
            return;
        }
    }

    private async Task HandleGetPlayerStatusAsync(GameSession session, GetPlayerStatusPacket packet)
    {
        if (packet.StatusType == GetPlayerStatusType.BasicStatus)
        {
            var mobileStatusPacket = new MobileStatusPacket(session.Mobile, 6, false);

            session.SendPackets(mobileStatusPacket);
        }

        if (packet.StatusType == GetPlayerStatusType.RequestSkills)
        {
            var skillsPacket = new SendSkillResponsePacket(session.Mobile, SendSkillResponseType.FullSkillListWithCap);

            session.SendPackets(skillsPacket);
        }
    }
}
