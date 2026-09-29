using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Broadcasts the supplied text as a system message to the local world's players.
/// </summary>
public sealed class BroadcastCommand : ICommandExecutor
{
    private readonly IBroadcastService _broadcast;
    private readonly ILocalizationService? _localization;

    public BroadcastCommand(IBroadcastService broadcast, ILocalizationService? localization = null)
    {
        _broadcast = broadcast;
        _localization = localization;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length == 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "broadcast <text>"));

            return;
        }

        var text = context.CommandLine.AsSpan().TrimStart()[context.CommandName.Length..].Trim().ToString();
        var sent = await _broadcast.BroadcastAsync(text, context.CancellationToken);
        context.Print(_localization.Text(CommandMessages.BroadcastSent, "Broadcast sent to {0} player(s).", sent));
    }
}
