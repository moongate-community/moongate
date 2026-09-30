using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Sets the weather the client shows (0x65): rain, storm, snow or none, how many particles (up to 70) and the
///     temperature, a signed byte.
/// </summary>
[PacketHandler(0x65, PacketSizing.Fixed, Length = 4)]
public sealed class WeatherPacket : BaseFixedPacket<WeatherPacket>, IOutgoingPacket
{
    public WeatherKindType Kind { get; }

    public int Density { get; }

    public int Temperature { get; }

    public WeatherPacket(WeatherKindType kind, int density, int temperature)
    {
        Kind = kind;
        Density = density;
        Temperature = temperature;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Kind);
        writer.WriteByte((byte)Density);
        writer.WriteByte((byte)(sbyte)Temperature);
    }
}
