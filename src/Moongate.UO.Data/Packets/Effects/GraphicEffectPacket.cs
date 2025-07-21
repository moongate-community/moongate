using Moongate.Core.Server.Packets;

namespace Moongate.UO.Data.Packets.Effects;

public class GraphicEffectPacket : BaseUoPacket
{
    public GraphicEffectPacket() : base(0x70)
    {
    }
}
