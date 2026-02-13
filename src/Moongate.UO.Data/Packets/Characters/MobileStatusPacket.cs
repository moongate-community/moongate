using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class MobileStatusPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }
    public int Version { get; set; }

    public bool CanBeRenamed { get; set; }

    public MobileStatusPacket() : base(0x11) { }

    public MobileStatusPacket(UOMobileEntity mobile, int version, bool canBeRenamed) : this()
    {
        Mobile = mobile;
        Version = version;
        CanBeRenamed = canBeRenamed;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Seek(2, SeekOrigin.Current); // Placeholder for length
        writer.Write(Mobile.Id.Value);
        writer.WriteAscii(Mobile.Name, 30);

        writer.WriteAttribute(Mobile.MaxHits, Mobile.Hits, Version == 0, true);
        writer.Write(CanBeRenamed);
        writer.Write((byte)Version);

        if (Version <= 0)
        {
            writer.WritePacketLength();

            return writer.ToArray();
        }

        writer.Write(Mobile.Gender == GenderType.Female);

        writer.Write((short)Mobile.Strength);
        writer.Write((short)Mobile.Dexterity);
        writer.Write((short)Mobile.Intelligence);

        writer.Write((short)Mobile.Stamina);
        writer.Write((short)Mobile.MaxStamina);

        writer.Write((short)Mobile.Mana);
        writer.Write((short)Mobile.MaxMana);

        writer.Write(Mobile.Gold);

        //TODO: Here is BodyWeight + TotalWeight
        writer.Write((short)1);

        if (Version >= 5)
        {
            // TODO: Here goes Max Weight
            writer.Write((short)1);
            writer.Write((byte)(Mobile.Race?.RaceID + 1 ?? 0));
        }

        // TODO: Here goes Stat CAP
        writer.Write((short)100);

        // TODO: Followers and Max Followers
        writer.Write((byte)0);
        writer.Write((byte)0);

        if (Version >= 4)
        {
            writer.Write((short)Mobile.FireResistance);
            writer.Write((short)Mobile.ColdResistance);
            writer.Write((short)Mobile.PoisonResistance);
            writer.Write((short)Mobile.EnergyResistance);
            writer.Write((short)Mobile.Luck);

            //TODO: Get Weapons Damage, now is hardcoded from 1 to 10

            writer.Write((short)1);
            writer.Write((short)10);

            writer.Write(0); //TODO: Tithing Point from Paladin book
        }

        if (Version >= 6)
        {
            for (var i = 0; i < 15; i++)
            {
                writer.Write((short)0);
            }
        }

        writer.WritePacketLength();

        return writer.ToArray();
    }
}
