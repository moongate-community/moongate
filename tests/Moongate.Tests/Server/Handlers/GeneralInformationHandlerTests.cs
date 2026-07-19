using Moongate.Server.Handlers;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class GeneralInformationHandlerTests
{
    [Fact]
    public void ParseLanguage_EmptyPayload_ReturnsNull()
        => Assert.Null(GeneralInformationHandler.ParseLanguage([]));

    [Fact]
    public void ParseLanguage_ReadsThreeCharCode()
        => Assert.Equal("ENU", GeneralInformationHandler.ParseLanguage([(byte)'E', (byte)'N', (byte)'U', 0x00]));

    [Fact]
    public void ParseScreenSize_ReadsWidthAndHeight()
    {
        // 2 bytes unknown, width 800 (0x0320), height 600 (0x0258).
        var payload = new byte[] { 0x00, 0x00, 0x03, 0x20, 0x02, 0x58 };

        var size = GeneralInformationHandler.ParseScreenSize(payload);

        Assert.Equal((800, 600), size);
    }

    [Fact]
    public void ParseScreenSize_ShortPayload_ReturnsNull()
        => Assert.Null(GeneralInformationHandler.ParseScreenSize([0x00, 0x00]));

    [Fact]
    public void ParseStatLockChange_LockAboveLocked_ClampsToUp()
        => Assert.Equal((StatType.Str, StatLockType.Up), GeneralInformationHandler.ParseStatLockChange([0, 9]));

    [Theory, InlineData(0, 0, StatType.Str, StatLockType.Up), InlineData(1, 1, StatType.Dex, StatLockType.Down),
     InlineData(2, 2, StatType.Int, StatLockType.Locked)]
    public void ParseStatLockChange_ReadsStatAndLock(
        byte stat,
        byte statLock,
        StatType expectedStat,
        StatLockType expectedLock
    )
        => Assert.Equal((expectedStat, expectedLock), GeneralInformationHandler.ParseStatLockChange([stat, statLock]));

    [Fact]
    public void ParseStatLockChange_ShortPayload_ReturnsNull()
        => Assert.Null(GeneralInformationHandler.ParseStatLockChange([0]));

    [Fact]
    public void ParseStatLockChange_UnknownStat_ReturnsNull()
        => Assert.Null(GeneralInformationHandler.ParseStatLockChange([7, 0]));
}
