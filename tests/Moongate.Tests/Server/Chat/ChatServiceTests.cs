using Moongate.Server.Services.Chat;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Chat;

public class ChatServiceTests
{
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
}
