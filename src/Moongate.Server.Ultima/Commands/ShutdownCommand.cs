using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Announces and requests an immediate or delayed graceful shutdown of this server instance.
/// </summary>
public sealed class ShutdownCommand : ICommandExecutor
{
    private readonly IServerShutdownService _shutdown;
    private readonly ITimerService _timers;
    private readonly IBroadcastService _broadcast;
    private readonly ILocalizationService? _localization;
    private int _scheduled;

    public ShutdownCommand(
        IServerShutdownService shutdown,
        ITimerService timers,
        IBroadcastService broadcast,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
        _shutdown = shutdown;
        _timers = timers;
        _broadcast = broadcast;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        var seconds = 0;

        if (context.Arguments.Length > 1 ||
            (context.Arguments.Length == 1 &&
             !int.TryParse(context.Arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out seconds)))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "shutdown [seconds] (0-2147483647)"));

            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();

        if (_shutdown.Requested.IsCompleted || Interlocked.CompareExchange(ref _scheduled, 1, 0) != 0)
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.ShutdownAlreadyScheduled,
                    "A shutdown is already scheduled or in progress."
                )
            );

            return;
        }

        try
        {
            var message = seconds == 0
                ? _localization.Text(CommandMessages.ShuttingDownNow, "The server is shutting down now.")
                : _localization.Text(CommandMessages.ShuttingDownIn, "The server will shut down in {0} seconds.", seconds);
            await _broadcast.BroadcastAsync(message, context.CancellationToken);

            if (seconds == 0)
            {
                _shutdown.RequestShutdown();
            }
            else
            {
                _timers.RegisterTimer("server-shutdown", TimeSpan.FromSeconds(seconds), _shutdown.RequestShutdown);
            }

            if (!context.IsInGame)
            {
                context.Print(message);
            }
        }
        catch
        {
            // A rejected notice or timer registration did not schedule shutdown; allow an administrator to retry.
            Volatile.Write(ref _scheduled, 0);

            throw;
        }
    }
}
