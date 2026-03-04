using System.Diagnostics;
using Humanizer;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Persistence;

[RegisterConsoleCommand(
    "save_persistence",
    "Saves the world immediately. Usage: save_persistence",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Administrator
)]
public class SavePersistenceCommand : ICommandExecutor
{
    private readonly IPersistenceService _persistenceService;

    private readonly ISpeechService _speechService;

    public SavePersistenceCommand(IPersistenceService persistenceService, ISpeechService speechService)
    {
        _persistenceService = persistenceService;
        _speechService = speechService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        var countdown = 5;

        while (countdown > 0)
        {
            _speechService.BroadcastFromServerAsync($"Saving world in {countdown} seconds...");
            context.Print($"Saving world in {countdown} seconds...");
            await Task.Delay(1000);
            countdown--;
        }

        var startTime = Stopwatch.GetTimestamp();

        await _persistenceService.SaveAsync();

        context.Print($"Saved world in {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds.Milliseconds().Humanize()}.");
        await _speechService.BroadcastFromServerAsync(
            $"World saved in {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds.Milliseconds().Humanize()}."
        );
    }
}
