using Moongate.Core.Types;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Sends a server-wide system message. The only real command in this feature — proves the
/// dispatcher end-to-end by reusing <see cref="IChatService.Broadcast" /> from the chat feature
/// rather than adding new domain logic. The <see cref="CommandAttribute" /> declares its metadata for
/// tooling (docs/source generation); the runtime dispatcher indexes it from the explicit
/// <c>RegisterCommand</c> call in <c>MoongateCommandsPlugin</c>, not by scanning the attribute.
/// </summary>
[Command(
    "broadcast|bc",
    AccountLevelType.GrandMaster,
    "Sends a server-wide system message.",
    Sources = CommandSourceType.InGame | CommandSourceType.Console | CommandSourceType.Rest
)]
public sealed class BroadcastCommand : ICommand
{
    private readonly IChatService _chat;

    public BroadcastCommand(IChatService chat)
    {
        _chat = chat;
    }

    public void Execute(CommandContext context)
    {
        if (context.Arguments.Count == 0)
        {
            context.Reply("Usage: broadcast <message>");

            return;
        }

        _chat.Broadcast(string.Join(' ', context.Arguments));
        context.Reply("Broadcast sent.");
    }
}
