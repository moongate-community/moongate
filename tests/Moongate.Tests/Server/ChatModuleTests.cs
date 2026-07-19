using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Scripting;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class ChatModuleTests
{
    private sealed class RecordingChatService : IChatService
    {
        public List<(string Text, Hue? Hue)> Broadcasts { get; } = [];
        public List<(Serial Speaker, ChatMessageType Type, string Text)> Said { get; } = [];

        public void Broadcast(string text, Hue? hue = null)
            => Broadcasts.Add((text, hue));

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
            => Said.Add((speaker.Id, type, text));
    }

    [Fact]
    public void Broadcast_ForwardsToChatService()
    {
        var (module, _, chat) = Build();

        module.Broadcast("Server restarting soon.");

        var (text, hue) = Assert.Single(chat.Broadcasts);
        Assert.Equal("Server restarting soon.", text);
        Assert.Null(hue);
    }

    [Fact]
    public void Say_ClassifiesEmoteBeforeForwarding()
    {
        var (module, persistence, chat) = Build();
        var mobile = new MobileEntity { Id = new(0x1), Name = "Guard", MapId = 0 };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).AsTask().Wait();

        var result = module.Say(mobile.Id.Value, "*nods*");

        Assert.True(result);
        var (speaker, type, text) = Assert.Single(chat.Said);
        Assert.Equal(mobile.Id, speaker);
        Assert.Equal(ChatMessageType.Emote, type);
        Assert.Equal("nods", text);
    }

    [Fact]
    public void Say_UnknownSerial_ReturnsFalse()
    {
        var (module, _, chat) = Build();

        Assert.False(module.Say(0xDEAD, "hello"));
        Assert.Empty(chat.Said);
    }

    private static (ChatModule Module, FakePersistenceService Persistence, RecordingChatService Chat) Build()
    {
        var persistence = new FakePersistenceService();
        var chat = new RecordingChatService();

        return (new(chat, persistence), persistence, chat);
    }
}
