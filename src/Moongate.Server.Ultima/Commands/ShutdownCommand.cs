using System.Globalization;
using Moongate.Server.Core.Data.Commands;
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
    private int _scheduled;

    public ShutdownCommand(IServerShutdownService shutdown, ITimerService timers, IBroadcastService broadcast)
    {
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
            context.PrintError("Usage: shutdown [seconds] (a whole number from 0 to 2147483647).");

            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();

        if (_shutdown.Requested.IsCompleted || Interlocked.CompareExchange(ref _scheduled, 1, 0) != 0)
        {
            context.PrintError("Shutdown is already scheduled or in progress.");

            return;
        }

        try
        {
            var message = seconds == 0
                ? "Server is shutting down now."
                : FormattableString.Invariant($"Server will shut down in {seconds} seconds.");
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
