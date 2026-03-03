using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;

namespace Moongate.Server.Commands;

/// <summary>
/// Locks console input until the configured unlock character is pressed.
/// </summary>
[RegisterConsoleCommand("lock|*", "Locks console input. Press '*' to unlock.")]
public sealed class LockCommand : ICommandExecutor
{
    private readonly IConsoleUiService _consoleUiService;

    public LockCommand(IConsoleUiService consoleUiService)
    {
        _consoleUiService = consoleUiService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        _consoleUiService.LockInput();
        context.Print("Console input is locked. Press '{0}' to unlock.", _consoleUiService.UnlockCharacter);

        return Task.CompletedTask;
    }
}
