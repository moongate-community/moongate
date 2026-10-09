using Moongate.Server.Ultima.Data.Schedule;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The arithmetic of the calendar: when a rule of
///     <c>
///         data/schedule.toml
///     </c>
///     occurs next in a time zone, and whether a day is
///     inside the window of a seasonal event. The rules are already checked by the loader.
/// </summary>
public static class ScheduleCalendar
{
    private static readonly DayOfWeek[] DaysOfTheWeek =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    private static readonly string[] DayNames = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

    /// <summary>
    ///     Gets the first instant after <paramref name="after" /> at which the rule occurs, with the clocks of the zone.
    /// </summary>
    /// <remarks>
    ///     A time the clocks skip runs at the first minute that exists; a time that happens twice runs the first time.
    /// </remarks>
    public static DateTimeOffset NextOccurrence(ScheduleWhen when, DateTimeOffset after, TimeZoneInfo zone)
    {
        return when.Every == "hour" ? NextHourly(when, after, zone) : NextDaily(when, after, zone);
    }

    /// <summary>
    ///     Gets whether a day is inside a window written
    ///     <c>
    ///         MM-dd
    ///     </c>
    ///     , both ends included; a window whose end is before its start
    ///     crosses the new year.
    /// </summary>
    public static bool IsInWindow(string from, string to, DateTime localDate)
    {
        var start = MonthDay(from);
        var end = MonthDay(to);
        var day = localDate.Month * 100 + localDate.Day;

        return start <= end ? day >= start && day <= end : day >= start || day <= end;
    }

    /// <summary>
    ///     Gets the next midnight of the zone strictly after <paramref name="after" />.
    /// </summary>
    public static DateTimeOffset NextMidnight(DateTimeOffset after, TimeZoneInfo zone)
    {
        return NextOccurrence(new ScheduleWhen { Every = "day", At = "00:00" }, after, zone);
    }

    // Every real hour of the clock, at the minute: stepping in real time keeps a repeated hour of the autumn.
    private static DateTimeOffset NextHourly(ScheduleWhen when, DateTimeOffset after, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(after, zone);
        var minute = int.Parse(when.At.AsSpan(1));
        var hourStart = after.ToUniversalTime() - TimeSpan.FromTicks(local.TimeOfDay.Ticks % TimeSpan.TicksPerHour);

        for (var hour = 0; hour < 3; hour++)
        {
            var candidate = hourStart.AddHours(hour).AddMinutes(minute);

            if (candidate > after)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No hourly occurrence found.");
    }

    // The wall clock of the zone, day by day, up to a year ahead.
    private static DateTimeOffset NextDaily(ScheduleWhen when, DateTimeOffset after, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(after, zone);
        var time = TimeOnly.ParseExact(when.At, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var days = when.Days.Count == 0
            ? DaysOfTheWeek.ToHashSet()
            : when.Days.Select(day => DaysOfTheWeek[Array.IndexOf(DayNames, day)]).ToHashSet();

        for (var step = 0; step < 370; step++)
        {
            var date = local.Date.AddDays(step);

            if (when.Every == "week" && !days.Contains(date.DayOfWeek))
            {
                continue;
            }

            var instant = ToInstant(DateTime.SpecifyKind(date + time.ToTimeSpan(), DateTimeKind.Unspecified), zone);

            if (instant > after)
            {
                return instant;
            }
        }

        throw new InvalidOperationException("No occurrence found in the next year.");
    }

    private static DateTimeOffset ToInstant(DateTime wall, TimeZoneInfo zone)
    {
        // A time the clocks skipped runs at the first minute that exists.
        while (zone.IsInvalidTime(wall))
        {
            wall = wall.AddMinutes(1);
        }

        // A time that happens twice runs the first time, at the larger offset.
        var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max() : zone.GetUtcOffset(wall);

        return new DateTimeOffset(wall, offset).ToUniversalTime();
    }

    private static int MonthDay(string text)
    {
        return int.Parse(text.AsSpan(0, 2)) * 100 + int.Parse(text.AsSpan(3, 2));
    }
}
