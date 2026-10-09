namespace Moongate.Tests.TestSupport.Ultima.Schedule;

/// <summary>
///     Time zones built in the tests, so no test depends on the tzdata of the machine.
/// </summary>
public static class TestZones
{
    /// <summary>
    ///     A zone of Europe: UTC+1, and UTC+2 from the last Sunday of March (the clocks go from 02:00 to 03:00) to the last Sunday
    ///     of
    ///     October (from 03:00 back to 02:00). In 2026: March 29 and October 25.
    /// </summary>
    public static TimeZoneInfo Europe { get; } = Create();

    private static TimeZoneInfo Create()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            3,
            5,
            DayOfWeek.Sunday
        );
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 3, 0, 0),
            10,
            5,
            DayOfWeek.Sunday
        );
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            start,
            end
        );

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/Europe",
            TimeSpan.FromHours(1),
            "Test",
            "Test standard",
            "Test daylight",
            [rule]
        );
    }
}
