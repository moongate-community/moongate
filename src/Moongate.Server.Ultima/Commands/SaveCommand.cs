using Moongate.Server.Core.Data.Commands;
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

    public SaveCommand(IWorldSaveService saves, IBroadcastService broadcast)
    {
        _saves = saves;
        _broadcast = broadcast;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError("Usage: save");

            return;
        }

        await _saves.SaveAsync(context.CancellationToken);
        await _broadcast.BroadcastAsync("world saved", context.CancellationToken);

        if (!context.IsInGame)
        {
            context.Print("world saved");
        }
    }
}
