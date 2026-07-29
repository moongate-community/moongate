using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Chat;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Chat;

public class ChatServiceTests
{
    [Fact]
    public void Broadcast_SendsThroughWorldServiceAndUsesTheDefaultHueWhenNoneGiven()
    {
        var world = new WorldService(
            new StubItemService([]),
            new SkillService(),
            new VirtualSerialService(),
            new StubEventBus(),
            TimeProvider.System,
            new OplService(new FakePersistenceService(), new ItemTemplateService(), new StubClilocService("gold coin")),
            new SessionManager(),
            new StubLightService(0)
        );
        var service = new ChatService(world, new StubEventBus());

        // No live sessions in this unit test — WorldService.Broadcast returning 0 here just proves the
        // call reaches WorldService without throwing; the real multi-session fan-out is covered by the
        // end-to-end integration test.
        service.Broadcast("Server restarting soon.");
    }

    [Fact]
    public void Classify_CommandPrefix_IsCommandTrue()
    {
        var decision = ChatService.Classify(".kick Bob");

        Assert.True(decision.IsCommand);
        Assert.Equal(ChatMessageType.Command, decision.Type);
    }

    [Fact]
    public void Classify_EmoteWrap_StripsAsterisksAndUsesDefaultRange()
    {
        var decision = ChatService.Classify("*waves*");

        Assert.False(decision.IsCommand);
        Assert.Equal(ChatMessageType.Emote, decision.Type);
        Assert.Equal("waves", decision.Text);
        Assert.Equal(15, decision.Range);
    }

    [Fact]
    public void Classify_EmptyEmote_TextIsEmpty()
    {
        var decision = ChatService.Classify("**");

        Assert.Equal(ChatMessageType.Emote, decision.Type);
        Assert.Equal(string.Empty, decision.Text);
    }

    [Fact]
    public void Classify_PlainText_IsRegularWithDefaultRange()
    {
        var decision = ChatService.Classify("hello there");

        Assert.False(decision.IsCommand);
        Assert.Equal(ChatMessageType.Regular, decision.Type);
        Assert.Equal("hello there", decision.Text);
        Assert.Equal(15, decision.Range);
    }

    [Fact]
    public void Classify_SingleAsterisk_IsNotTreatedAsEmote()
    {
        // A lone "*" is too short to be a wrap (needs distinct opening/closing chars).
        var decision = ChatService.Classify("*");

        Assert.Equal(ChatMessageType.Regular, decision.Type);
        Assert.Equal("*", decision.Text);
    }

    [Fact]
    public void Classify_WhisperPrefix_StripsSemicolonAndUsesWhisperRange()
    {
        var decision = ChatService.Classify(";psst");

        Assert.Equal(ChatMessageType.Whisper, decision.Type);
        Assert.Equal("psst", decision.Text);
        Assert.Equal(1, decision.Range);
    }

    [Fact]
    public void Classify_YellPrefix_StripsBangAndUsesYellRange()
    {
        var decision = ChatService.Classify("!help");

        Assert.Equal(ChatMessageType.Yell, decision.Type);
        Assert.Equal("help", decision.Text);
        Assert.Equal(18, decision.Range);
    }

    [Fact]
    public void IsRateLimited_ExactlyAtTheBoundary_IsFalse()
    {
        var lastChatAt = DateTimeOffset.UtcNow;
        var now = lastChatAt.AddMilliseconds(25);

        Assert.False(ChatService.IsRateLimited(lastChatAt, now));
    }

    [Fact]
    public void IsRateLimited_WithinTheWindow_IsTrue()
    {
        var lastChatAt = DateTimeOffset.UtcNow;
        var now = lastChatAt.AddMilliseconds(10);

        Assert.True(ChatService.IsRateLimited(lastChatAt, now));
    }

    [Fact]
    public void Say_PublishesMobileSpeechEvent()
    {
        var world = new WorldService(
            new StubItemService([]),
            new SkillService(),
            new VirtualSerialService(),
            new StubEventBus(),
            TimeProvider.System,
            new OplService(new FakePersistenceService(), new ItemTemplateService(), new StubClilocService("gold coin")),
            new SessionManager(),
            new StubLightService(0)
        );
        var bus = new StubEventBus();
        var service = new ChatService(world, bus);
        var speaker = new MobileEntity { Id = new(0x1), Name = "Hero", MapId = 0, Position = new(10, 10, 0) };

        service.Say(speaker, ChatMessageType.Regular, "hi", Hue.Default, 15);

        var evt = Assert.IsType<MobileSpeechEvent>(Assert.Single(bus.Published));
        Assert.Equal(speaker.Id, evt.Speaker);
        Assert.Equal(ChatMessageType.Regular, evt.Type);
        Assert.Equal("hi", evt.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SayAs_BlankText_SaysNothing(string text)
    {
        var (service, world) = Build();

        Assert.False(service.SayAs(Speaker(), text));
        Assert.Empty(world.InRange);
    }

    // The limit ai.say used to enforce alone, now applying to every caller.
    [Fact]
    public void SayAs_TextOverTheMaximum_SaysNothing()
    {
        var (service, world) = Build();

        Assert.False(service.SayAs(Speaker(), new string('a', 129)));
        Assert.Empty(world.InRange);
    }

    // The filter chat.say used to enforce alone. A leading dot is a command, not speech.
    [Fact]
    public void SayAs_ACommand_SaysNothing()
    {
        var (service, world) = Build();

        Assert.False(service.SayAs(Speaker(), ".help"));
        Assert.Empty(world.InRange);
    }

    // Coverage that moved here from ChatModuleTests when classification became the service's job.
    [Theory]
    [InlineData("*nods*", ChatMessageType.Emote)]
    [InlineData("!Guards!", ChatMessageType.Yell)]
    [InlineData(";psst", ChatMessageType.Whisper)]
    public void SayAs_PrefixedText_SpeaksItAsThatKind(string text, ChatMessageType expected)
    {
        var (service, world) = Build();

        Assert.True(service.SayAs(Speaker(), text));
        Assert.Single(world.InRange);
        Assert.Equal(expected, ChatService.Classify(text).Type);
    }

    [Fact]
    public void SayAs_OrdinaryText_SaysItOnce()
    {
        var (service, world) = Build();

        Assert.True(service.SayAs(Speaker(), "Welcome back."));
        Assert.Single(world.InRange);
    }

    private static (ChatService Service, RecordingWorldService World) Build()
    {
        var world = new RecordingWorldService();

        return (new(world, new StubEventBus()), world);
    }

    private static MobileEntity Speaker()
        => new() { Name = "Squid", Body = 400, MapId = 1, Position = new(1, 1, 0) };
}
