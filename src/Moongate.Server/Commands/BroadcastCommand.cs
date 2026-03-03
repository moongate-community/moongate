using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Commands;

/// <summary>
/// Sends a server-wide broadcast message.
/// </summary>
[RegisterConsoleCommand(
    "broadcast|bc",
    "Send a server message to all active sessions. Usage: broadcast <message>",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Administrator
)]
public sealed class BroadcastCommand : ICommandExecutor
{
    private readonly ISpeechService _speechService;

    public BroadcastCommand(ISpeechService speechService)
    {
        _speechService = speechService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length == 0)
        {
            context.Print("Usage: broadcast <message>");

            return;
        }

        var message = string.Join(' ', context.Arguments);
        var recipients = await _speechService.BroadcastFromServerAsync("SERVER: " + message, SpeechHues.Orange);

        context.Print("Broadcast sent to {0} session(s).", recipients);
    }
}
