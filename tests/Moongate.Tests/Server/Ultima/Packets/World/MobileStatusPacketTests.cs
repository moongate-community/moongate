using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MobileStatusPacketTests
{
    [Fact]
    public void Encode_WritesTheVersion5StatusOf91Bytes()
    {
        var status = new MobileStatusInfo
        {
            Serial = new Serial(0x00000002), Name = "Aria", Hits = 55, HitsMax = 60, Female = true, Strength = 60,
            Dexterity = 20, Intelligence = 10, Stamina = 18, StaminaMax = 20, Mana = 9, ManaMax = 10, Gold = 1000,
            PhysicalResistance = 3, Weight = 42, MaxWeight = 275, Race = RaceType.Elf, StatCap = 225, Followers = 0,
            FollowersMax = 5, FireResistance = 1, ColdResistance = 2, PoisonResistance = 4, EnergyResistance = 5, Luck = 7,
            DamageMin = 1, DamageMax = 4, TithingPoints = 12
        };

        var bytes = PacketCodec.Encode(new MobileStatusPacket(status));

        var name = new byte[30];
        "Aria"u8.CopyTo(name);
        Assert.Equal(
            Convert.FromHexString(
                "11" + "005B" + "00000002" + Convert.ToHexString(name) + "0037" + "003C" + "00" + "05" + "01" +
                "003C" + "0014" + "000A" + "0012" + "0014" + "0009" + "000A" + "000003E8" + "0003" + "002A" +
                "0113" + "02" + "00E1" + "00" + "05" +
                "0001" + "0002" + "0004" + "0005" + "0007" + "0001" + "0004" + "0000000C"
            ),
            bytes
        );
        Assert.Equal(91, bytes.Length);
    }
}
