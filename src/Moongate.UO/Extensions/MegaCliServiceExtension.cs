using Moongate.UO.Data.Ids;
using Moongate.UO.Interfaces.Services;

namespace Moongate.UO.Extensions;

public static class MegaCliServiceExtension
{
    public static async Task<MegaClilocResponsePacket> ToPacket(this IMegaClilocService service, Serial serial)
    {
        var packet = new MegaClilocResponsePacket();

        var entry = await service.GetMegaClilocEntryAsync(serial);

        packet.Serial = serial;
        packet.Properties = entry.Properties;

        return packet;
    }
}
