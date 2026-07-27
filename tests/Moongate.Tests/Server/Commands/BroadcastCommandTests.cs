using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands;

public class BroadcastCommandTests
{
    private sealed class RecordingChatService : IChatService
    {
        public List<string> Broadcasts { get; } = [];

        public void Broadcast(string text, Hue? hue = null)
            => Broadcasts.Add(text);

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
        {
        }
    }

    [Fact]
    public void Execute_NoArguments_RepliesUsageAndDoesNotBroadcast()
    {
        var chat = new RecordingChatService();
        var command = new BroadcastCommand(chat);
        var replies = new List<string>();
        var context = new CommandContext(CommandSourceType.InGame, new(), [], replies.Add);

        command.Execute(context);

        Assert.Empty(chat.Broadcasts);
        Assert.Equal("Usage: broadcast <message>", Assert.Single(replies));
    }

    [Fact]
    public void Execute_WithArguments_JoinsThemAndBroadcasts()
    {
        var chat = new RecordingChatService();
        var command = new BroadcastCommand(chat);
        var replies = new List<string>();
        var context = new CommandContext(
            CommandSourceType.InGame,
            new(),
            ["Server", "restarting", "soon"],
            replies.Add
        );

        command.Execute(context);

        Assert.Equal("Server restarting soon", Assert.Single(chat.Broadcasts));
        Assert.Equal("Broadcast sent.", Assert.Single(replies));
    }
}
