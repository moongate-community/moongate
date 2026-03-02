using Moongate.Server.Interfaces.Services.World;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Executes door generation at server startup to simplify door debugging workflows.
/// </summary>
public sealed class WorldGenerationStartupService : IWorldGenerationStartupService
{
    private readonly ILogger _logger = Log.ForContext<WorldGenerationStartupService>();
    private readonly IWorldGeneratorBuilderService _worldGeneratorBuilderService;

    public WorldGenerationStartupService(IWorldGeneratorBuilderService worldGeneratorBuilderService)
    {
        _worldGeneratorBuilderService = worldGeneratorBuilderService;
    }

    public async Task StartAsync()
    {
        _logger.Information("Startup world generation enabled. Running generator: doors.");

        await _worldGeneratorBuilderService.GenerateAsync(
            "doors",
            message => _logger.Information("[WorldGen] {Message}", message)
        );
    }

    public Task StopAsync()
        => Task.CompletedTask;
}
