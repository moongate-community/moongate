using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Tests.TestSupport.Ultima.Schedule;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ScheduleCalendarTests
{
    private static readonly TimeZoneInfo Zone = TestZones.Europe;

    [Fact]
    public void Day_LaterToday()
    {
        // 03:00 local, the rule is 04:00 local = 03:00Z.
        Assert.Equal(Utc(2026, 1, 10, 3), ScheduleCalendar.NextOccurrence(Day("04:00"), Utc(2026, 1, 10, 2), Zone));
    }

    [Fact]
    public void Day_AtTheSameInstant_IsTomorrow()
    {
        Assert.Equal(Utc(2026, 1, 11, 3), ScheduleCalendar.NextOccurrence(Day("04:00"), Utc(2026, 1, 10, 3), Zone));
    }

    [Fact]
    public void Day_RollsOverTheEndOfTheMonthAndOfTheYear()
    {
        Assert.Equal(Utc(2027, 1, 1, 3), ScheduleCalendar.NextOccurrence(Day("04:00"), Utc(2026, 12, 31, 5), Zone));
        Assert.Equal(Utc(2026, 3, 1, 3), ScheduleCalendar.NextOccurrence(Day("04:00"), Utc(2026, 2, 28, 5), Zone));
    }

    [Fact]
    public void Day_KnowsTheLeapDay()
    {
        Assert.Equal(Utc(2028, 2, 29, 3), ScheduleCalendar.NextOccurrence(Day("04:00"), Utc(2028, 2, 28, 12), Zone));
    }

    [Fact]
    public void Hour_AtHalfPast()
    {
        var rule = new ScheduleWhen { Every = "hour", At = ":30" };

        Assert.Equal(Utc(2026, 1, 10, 10, 30), ScheduleCalendar.NextOccurrence(rule, Utc(2026, 1, 10, 10, 10), Zone));
        Assert.Equal(Utc(2026, 1, 10, 11, 30), ScheduleCalendar.NextOccurrence(rule, Utc(2026, 1, 10, 10, 30), Zone));
    }

    [Fact]
    public void Week_Sunday_FromAWednesday()
    {
        var rule = new ScheduleWhen { Every = "week", At = "18:00", Days = ["sun"] };

        // 2026-01-07 is a Wednesday; Sunday January 11 at 18:00 local is 17:00Z.
        Assert.Equal(Utc(2026, 1, 11, 17), ScheduleCalendar.NextOccurrence(rule, Utc(2026, 1, 7, 12), Zone));
    }

    [Fact]
    public void Week_WithNoDays_IsEveryDay()
    {
        var rule = new ScheduleWhen { Every = "week", At = "04:00" };

        Assert.Equal(Utc(2026, 1, 10, 3), ScheduleCalendar.NextOccurrence(rule, Utc(2026, 1, 10, 2), Zone));
        Assert.Equal(Utc(2026, 1, 11, 3), ScheduleCalendar.NextOccurrence(rule, Utc(2026, 1, 10, 3), Zone));
    }

    [Fact]
    public void Week_ManyDays_TakesTheNearest()
    {
        var rule = new ScheduleWhen { Every = "week", At = "09:00", Days = ["sat", "mon"] };

        // After Sunday 2026-01-11 12:00Z the nearest is Monday 09:00 local = 08:00Z.
        Assert.Equal(Utc(2026, 1, 12, 8), ScheduleCalendar.NextOccurrence(rule, Utc(2026, 1, 11, 12), Zone));
    }

    [Fact]
    public void Spring_ATimeThatDoesNotExist_RunsAtTheFirstMinuteThatDoes()
    {
        // 2026-03-29: 02:00-02:59 local do not exist. 02:30 runs at 03:00 local (UTC+2) = 01:00Z.
        Assert.Equal(Utc(2026, 3, 29, 1), ScheduleCalendar.NextOccurrence(Day("02:30"), Utc(2026, 3, 28, 12), Zone));
    }

    [Fact]
    public void Spring_TheDayAfter_IsBackOnTheClock()
    {
        // 2026-03-30 02:30 local (UTC+2) = 00:30Z.
        Assert.Equal(Utc(2026, 3, 30, 0, 30), ScheduleCalendar.NextOccurrence(Day("02:30"), Utc(2026, 3, 29, 2), Zone));
    }

    [Fact]
    public void Autumn_ATimeThatHappensTwice_RunsOnlyTheFirstTime()
    {
        // 2026-10-25: 02:00-02:59 local happen twice. 02:30 the first time is UTC+2 = 00:30Z.
        var first = ScheduleCalendar.NextOccurrence(Day("02:30"), Utc(2026, 10, 24, 12), Zone);

        Assert.Equal(Utc(2026, 10, 25, 0, 30), first);
        // The second 02:30 (01:30Z) is not an occurrence; the next one is the next day at UTC+1 = 01:30Z.
        Assert.Equal(Utc(2026, 10, 26, 1, 30), ScheduleCalendar.NextOccurrence(Day("02:30"), first, Zone));
        Assert.Equal(
            Utc(2026, 10, 26, 1, 30),
            ScheduleCalendar.NextOccurrence(Day("02:30"), Utc(2026, 10, 25, 1, 45), Zone)
        );
    }

    [Fact]
    public void Hour_OverTheAutumnRepeat_RunsEveryRealHour()
    {
        var rule = new ScheduleWhen { Every = "hour", At = ":30" };
        var first = ScheduleCalendar.NextOccurrence(rule, Utc(2026, 10, 25, 0, 30), Zone);

        Assert.Equal(Utc(2026, 10, 25, 1, 30), first);
        Assert.Equal(Utc(2026, 10, 25, 2, 30), ScheduleCalendar.NextOccurrence(rule, first, Zone));
    }

    [Fact]
    public void Utc_ZoneWithoutRules()
    {
        Assert.Equal(
            Utc(2026, 1, 10, 4),
            ScheduleCalendar.NextOccurrence(Day("04:00"), Utc(2026, 1, 10, 3), TimeZoneInfo.Utc)
        );
    }

    [Theory,
     InlineData("10-19", false),
     InlineData("10-20", true),
     InlineData("11-02", true),
     InlineData("11-03", false)]
    public void IsInWindow_InsideTheYear(string day, bool expected)
    {
        Assert.Equal(expected, ScheduleCalendar.IsInWindow("10-20", "11-02", Date(day)));
    }

    [Theory,
     InlineData("12-19", false),
     InlineData("12-20", true),
     InlineData("12-31", true),
     InlineData("01-01", true),
     InlineData("01-06", true),
     InlineData("01-07", false),
     InlineData("07-04", false)]
    public void IsInWindow_CrossingTheNewYear(string day, bool expected)
    {
        Assert.Equal(expected, ScheduleCalendar.IsInWindow("12-20", "01-06", Date(day)));
    }

    [Fact]
    public void IsInWindow_ASingleDay_IsTheLeapDay()
    {
        Assert.True(ScheduleCalendar.IsInWindow("02-29", "02-29", new DateTime(2028, 2, 29)));
        Assert.False(ScheduleCalendar.IsInWindow("02-29", "02-29", new DateTime(2027, 3, 1)));
    }

    [Fact]
    public void NextMidnight_IsTheNextLocalMidnight_AcrossDaylightSaving()
    {
        Assert.Equal(Utc(2026, 3, 28, 23), ScheduleCalendar.NextMidnight(Utc(2026, 3, 28, 12), Zone));
        Assert.Equal(Utc(2026, 3, 29, 22), ScheduleCalendar.NextMidnight(Utc(2026, 3, 29, 12), Zone));
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }

    private static ScheduleWhen Day(string at)
    {
        return new ScheduleWhen { Every = "day", At = at };
    }

    // The year does not matter to a window; 2027 is not a leap year.
    private static DateTime Date(string day)
    {
        return new DateTime(2027, int.Parse(day[..2]), int.Parse(day[3..]));
    }
}
