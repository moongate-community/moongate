using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Commands;

/// <summary>
///     Prints how long the server has been running and since when, <c>uptime</c>.
/// </summary>
public sealed class UptimeCommand : ICommandExecutor
{
    private readonly IAdminServerInfoProvider _info;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    public UptimeCommand(IAdminServerInfoProvider info, TimeProvider time, ILocalizationService? localization = null)
    {
        _info = info;
        _time = time;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "uptime"));

            return Task.CompletedTask;
        }

        // A clock set back after the start: never a negative time.
        var uptime = _info.GetSnapshot().Uptime;
        uptime = uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
        var since = _time.GetUtcNow().UtcDateTime - uptime;
        context.Print(
            _localization.Text(
                CommandMessages.UptimeText,
                "Up for {0}, since {1}.",
                Duration(uptime),
                since.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            )
        );

        return Task.CompletedTask;
    }

    // "2d 4h 13m", "4h 13m", "13m 9s" or "9s": seconds only while they still matter.
    private static string Duration(TimeSpan time)
    {
        var days = (int)time.TotalDays;

        if (days > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{days}d {time.Hours}h {time.Minutes}m");
        }

        if (time.Hours > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{time.Hours}h {time.Minutes}m");
        }

        return time.Minutes > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{time.Minutes}m {time.Seconds}s")
            : string.Create(CultureInfo.InvariantCulture, $"{time.Seconds}s");
    }
}
