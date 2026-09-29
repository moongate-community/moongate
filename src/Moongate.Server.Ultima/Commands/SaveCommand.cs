using System.Diagnostics;
using System.Globalization;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Saves the world and announces success after durable persistence completes.
/// </summary>
public sealed class SaveCommand : ICommandExecutor
{
    private readonly IWorldSaveService _saves;
    private readonly IBroadcastService _broadcast;
    private readonly ILocalizationService? _localization;

    public SaveCommand(IWorldSaveService saves, IBroadcastService broadcast, ILocalizationService? localization = null)
    {
        _localization = localization;
        _saves = saves;
        _broadcast = broadcast;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "save"));

            return;
        }

        var start = Stopwatch.GetTimestamp();
        await _saves.SaveAsync(context.CancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(start);
        var message = _localization.Text(
            CommandMessages.WorldSaved,
            "The world has been saved in {0} seconds.",
            elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture)
        );
        await _broadcast.BroadcastAsync(message, context.CancellationToken);

        if (!context.IsInGame)
        {
            context.Print(message);
        }
    }
}
