using System.Diagnostics;
using Humanizer;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Runs world door generation.
/// </summary>
[RegisterConsoleCommand(
    "spawn_doors|.spawn_doors",
    "Run door world generation immediately. Usage: .spawn_doors",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Administrator
)]
public sealed class SpawnDoorsCommand : ICommandExecutor
{
    private readonly IWorldGeneratorBuilderService _worldGeneratorBuilderService;

    public SpawnDoorsCommand(IWorldGeneratorBuilderService worldGeneratorBuilderService)
    {
        _worldGeneratorBuilderService = worldGeneratorBuilderService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length > 0)
        {
            context.Print("Usage: .spawn_doors");

            return;
        }

        try
        {
            var startTime = Stopwatch.GetTimestamp();
            context.Print("Starting door generation...");
            await Task.Delay(1000);
            await _worldGeneratorBuilderService.GenerateAsync("doors", message => context.Print("{0}", message));
            var endTime = Stopwatch.GetElapsedTime(startTime);
            context.Print($"Door generation finished in {endTime.TotalMilliseconds.Milliseconds().Humanize()}");
        }
        catch (Exception ex)
        {
            context.Print("Door generation failed: {0}", ex.Message);
        }
    }
}
