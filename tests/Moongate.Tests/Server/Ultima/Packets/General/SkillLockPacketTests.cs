using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class SkillLockPacketTests
{
    [Fact]
    public void TryParse_ReadsTheSkillAndTheLock()
    {
        // skill 21 (hiding), locked down
        Assert.True(SkillLockPacket.TryParse(Convert.FromHexString("3A00060015" + "01"), out var packet));

        Assert.Equal((21, (byte)1, 6), (packet.Skill, packet.Lock, packet.Length));
    }

    [Fact]
    public void TryParse_AValueOutsideTheEnums_IsKeptAsTheClientSentIt()
    {
        Assert.True(SkillLockPacket.TryParse(Convert.FromHexString("3A000601F7" + "09"), out var packet));

        Assert.Equal((503, (byte)9), (packet.Skill, packet.Lock));
    }

    [Theory]
    [InlineData("3A0005")]
    [InlineData("3A000500")]
    [InlineData("3A0006001")]
    public void TryParse_TooShort_Fails(string hex)
    {
        Assert.False(SkillLockPacket.TryParse(Convert.FromHexString(hex.Length % 2 == 0 ? hex : hex + "0"), out _));
    }
}
