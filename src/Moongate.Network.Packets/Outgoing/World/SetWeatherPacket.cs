using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.World;

[PacketHandler(0x65, PacketSizing.Fixed, Length = 4, Description = "Set Weather")]

/// <summary>
/// Represents Set Weather packet (0x65).
/// </summary>
public class SetWeatherPacket : BaseGameNetworkPacket
{
    /// <summary>
    /// Client maximum number of weather effects shown on screen.
    /// </summary>
    public const byte MaximumEffectsOnScreen = 70;

    public WeatherType Type { get; set; }

    public byte EffectCount { get; set; }

    public byte Temperature { get; set; }

    public SetWeatherPacket()
        : base(0x65, 4) { }

    public SetWeatherPacket(WeatherType type, byte effectCount, byte temperature)
        : this()
    {
        Type = type;
        EffectCount = effectCount;
        Temperature = temperature;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)Type);
        writer.Write(EffectCount);
        writer.Write(Temperature);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 3)
        {
            return false;
        }

        Type = (WeatherType)reader.ReadByte();
        EffectCount = reader.ReadByte();
        Temperature = reader.ReadByte();

        return reader.Remaining == 0;
    }
}
