using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class SkillsPacketTests
{
    [Fact]
    public void TheWholeList_NumbersTheSkillsFromOne_AndEndsWithZero()
    {
        MobileSkill[] skills =
        [
            new() { Skill = SkillType.Alchemy, Base = 505, Cap = 1000, Lock = SkillLockType.Up },
            new() { Skill = SkillType.Magery, Base = 1000, Cap = 1200, Lock = SkillLockType.Locked }
        ];

        var bytes = PacketCodec.Encode(SkillsPacket.All(skills));

        Assert.Equal(
            Convert.FromHexString(
                "3A" + "0018" + "02" +
                "0001" + "01F9" + "01F9" + "00" + "03E8" +
                "001A" + "03E8" + "03E8" + "02" + "04B0" +
                "0000"
            ),
            bytes
        );
    }

    [Fact]
    public void OneSkill_NumbersItFromZero()
    {
        var bytes = PacketCodec.Encode(
            SkillsPacket.One(new() { Skill = SkillType.Magery, Base = 1000, Cap = 1200, Lock = SkillLockType.Down })
        );

        Assert.Equal(Convert.FromHexString("3A" + "000D" + "DF" + "0019" + "03E8" + "03E8" + "01" + "04B0"), bytes);
    }
}
