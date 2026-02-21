using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x11, PacketSizing.Variable, Description = "Status Bar Info")]
public class PlayerStatusPacket : BaseGameNetworkPacket
{
    public bool CanBeRenamed { get; set; }

    public ushort CurrentHits { get; set; }

    public ushort MaxHits { get; set; }

    public UOMobileEntity? Mobile { get; set; }

    public string Name { get; set; } = string.Empty;

    public Serial Serial { get; set; }

    public byte Version { get; set; }

    public PlayerStatusPacket()
        : base(0x11, -1) { }

    public PlayerStatusPacket(UOMobileEntity mobile, byte version = 0, bool canBeRenamed = false)
        : this()
    {
        Mobile = mobile;
        CanBeRenamed = canBeRenamed;
        Version = version;
    }

    public override void Write(ref SpanWriter writer)
    {
        if (Mobile is null)
        {
            throw new InvalidOperationException("Mobile must be set before writing PlayerStatusPacket.");
        }

        var maxHits = Mobile.MaxHits > 0 ? Mobile.MaxHits : Math.Max(1, Mobile.Hits);
        var currentHits = Math.Clamp(Mobile.Hits, 0, maxHits);

        writer.Write(OpCode);
        writer.Write((ushort)0); // Placeholder for length
        writer.Write(Mobile.Id.Value);
        writer.WriteAscii(Mobile.Name ?? string.Empty, 30);
        writer.WriteAttribute(maxHits, currentHits, normalize: true, reverse: true);
        writer.Write(CanBeRenamed);
        writer.Write(Version);

        if (Version >= 1)
        {
            writer.Write(Mobile.Gender == Moongate.UO.Data.Types.GenderType.Female);
            writer.Write((ushort)Mobile.Strength);
            writer.Write((ushort)Mobile.Dexterity);
            writer.Write((ushort)Mobile.Intelligence);
            writer.Write((ushort)Mobile.Stamina);
            writer.Write((ushort)Mobile.MaxStamina);
            writer.Write((ushort)Mobile.Mana);
            writer.Write((ushort)Mobile.MaxMana);
            writer.Write(0); // Gold
            writer.Write((ushort)0); // Resistance
            writer.Write((ushort)0); // Weight
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 42)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != Length)
        {
            return false;
        }

        Serial = (Serial)reader.ReadUInt32();
        Name = reader.ReadAscii(30);
        CurrentHits = reader.ReadUInt16();
        MaxHits = reader.ReadUInt16();
        CanBeRenamed = reader.ReadBoolean();
        Version = reader.ReadByte();

        return reader.Remaining == 0;
    }
}
