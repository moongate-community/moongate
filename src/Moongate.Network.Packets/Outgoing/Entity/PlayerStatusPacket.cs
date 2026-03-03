using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x11, PacketSizing.Variable, Description = "Status Bar Info")]

/// <summary>
/// Represents PlayerStatusPacket.
/// </summary>
public class PlayerStatusPacket : BaseGameNetworkPacket
{
    public bool CanBeRenamed { get; set; }

    public ushort CurrentHits { get; set; }

    public ushort MaxHits { get; set; }

    public UOMobileEntity? Mobile { get; set; }

    public string Name { get; set; }

    public Serial Serial { get; set; }

    public byte Version { get; set; }

    public PlayerStatusPacket()
        : base(0x11) { }

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
        writer.WriteAttribute(maxHits, currentHits, true, true);
        writer.Write(CanBeRenamed);
        writer.Write(Version);

        if (Version >= 1)
        {
            writer.Write(Mobile.Gender == GenderType.Female);
            writer.Write((ushort)Mobile.Strength);
            writer.Write((ushort)Mobile.Dexterity);
            writer.Write((ushort)Mobile.Intelligence);
            writer.Write((ushort)Mobile.Stamina);
            writer.Write((ushort)Mobile.MaxStamina);
            writer.Write((ushort)Mobile.Mana);
            writer.Write((ushort)Mobile.MaxMana);
            writer.Write(0);         // Gold
            writer.Write((ushort)0); // Resistance
            writer.Write((ushort)0); // Weight
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 42)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length)
        {
            return false;
        }

        Serial = (Serial)reader.ReadUInt32();
        Name = reader.ReadAscii(30);
        CurrentHits = reader.ReadUInt16();
        MaxHits = reader.ReadUInt16();
        CanBeRenamed = reader.ReadBoolean();
        Version = reader.ReadByte();

        if (Version >= 1)
        {
            if (reader.Remaining < 24)
            {
                return false;
            }

            _ = reader.ReadBoolean(); // Sex+Race
            _ = reader.ReadUInt16();  // Strength
            _ = reader.ReadUInt16();  // Dexterity
            _ = reader.ReadUInt16();  // Intelligence
            _ = reader.ReadUInt16();  // Current Stamina
            _ = reader.ReadUInt16();  // Max Stamina
            _ = reader.ReadUInt16();  // Current Mana
            _ = reader.ReadUInt16();  // Max Mana
            _ = reader.ReadUInt32();  // Gold
            _ = reader.ReadUInt16();  // Resistance
            _ = reader.ReadUInt16();  // Weight
        }

        return reader.Remaining == 0;
    }
}
