using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Lifecycle;

namespace Moongate.Server.Commands;

/// <summary>
/// Requests server shutdown.
/// </summary>
[RegisterConsoleCommand("exit|shutdown", "Requests server shutdown.")]
public sealed class ExitCommand : ICommandExecutor
{
    private readonly IServerLifetimeService _serverLifetimeService;

    public ExitCommand(IServerLifetimeService serverLifetimeService)
    {
        _serverLifetimeService = serverLifetimeService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        context.Print("Shutdown requested by console command.");
        _serverLifetimeService.RequestShutdown();

        return Task.CompletedTask;
    }
}
