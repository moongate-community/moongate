using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Broadcasts the supplied text as a system message to the local world's players.
/// </summary>
public sealed class BroadcastCommand : ICommandExecutor
{
    private readonly IBroadcastService _broadcast;

    public BroadcastCommand(IBroadcastService broadcast)
    {
        _broadcast = broadcast;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length == 0)
        {
            context.PrintError("Usage: broadcast <text>");

            return;
        }

        var text = context.CommandLine.AsSpan().TrimStart()[context.CommandName.Length..].Trim().ToString();
        var sent = await _broadcast.BroadcastAsync(text, context.CancellationToken);
        context.Print("Broadcast queued for {0} player(s).", sent);
    }
}
