using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Modules.Internal;

namespace Moongate.Tests.Server.Ultima.Modules.Internal;

public sealed class AttendanceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryAttend_TheFirstAsks_IsTrue_AndTheOthersWithinHalfASecondAreNot()
    {
        var attendance = new Attendance();

        Assert.True(attendance.TryAttend(new Serial(2), Start));
        Assert.False(attendance.TryAttend(new Serial(2), Start.AddMilliseconds(100)));
        Assert.False(attendance.TryAttend(new Serial(2), Start.AddMilliseconds(499)));
        Assert.True(attendance.TryAttend(new Serial(2), Start.AddMilliseconds(600)));
    }

    [Fact]
    public void TryAttend_ForgetsNoOneElse_AndEachPlayerHasItsOwnTurn()
    {
        var attendance = new Attendance();

        Assert.True(attendance.TryAttend(new Serial(2), Start));
        Assert.True(attendance.TryAttend(new Serial(3), Start));
    }

    [Fact]
    public void TryAttend_TheClockGoingBack_IsServedAgain()
    {
        var attendance = new Attendance();
        attendance.TryAttend(new Serial(2), Start);

        Assert.True(attendance.TryAttend(new Serial(2), Start.AddSeconds(-5)));
    }

    [Fact]
    public void TryAttend_ManyPlayers_StillWorksAfterTheOldOnesAreForgotten()
    {
        var attendance = new Attendance();

        for (uint player = 1; player <= 400; player++)
        {
            Assert.True(attendance.TryAttend(new Serial(player), Start));
        }

        Assert.True(attendance.TryAttend(new Serial(1), Start.AddSeconds(2)));
    }
}
