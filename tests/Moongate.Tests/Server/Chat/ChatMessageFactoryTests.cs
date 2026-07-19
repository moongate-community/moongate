using Moongate.Core.Primitives;
using Moongate.Server.Services.Chat;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Chat;

public class ChatMessageFactoryTests
{
    [Fact]
    public void CreateFromMobile_DefaultHue_FallsBackToChatHuesDefault()
    {
        var packet = ChatMessageFactory.CreateFromMobile(
            new(0x1),
            "Hero",
            400,
            ChatMessageType.Regular,
            Hue.Default,
            "Hi"
        );

        Assert.Equal(ChatHues.Default, packet.Hue);
    }

    [Fact]
    public void CreateFromMobile_ExplicitHue_IsPreserved()
    {
        var packet = ChatMessageFactory.CreateFromMobile(
            new(0x1),
            "Hero",
            400,
            ChatMessageType.Regular,
            new(0x21),
            "Hi"
        );

        Assert.Equal(new Hue(0x21), packet.Hue);
    }

    [Fact]
    public void CreateSystem_DefaultsToBroadcastHueAndSystemSender()
    {
        var packet = ChatMessageFactory.CreateSystem("Server restarting soon.");

        Assert.Equal(Serial.Zero, packet.Speaker);
        Assert.Equal(ChatMessageType.System, packet.Type);
        Assert.Equal(ChatHues.Broadcast, packet.Hue);
        Assert.Equal("System", packet.SpeakerName);
        Assert.Equal("Server restarting soon.", packet.Text);
    }

    [Fact]
    public void CreateSystem_ExplicitHue_OverridesTheDefault()
    {
        var packet = ChatMessageFactory.CreateSystem("Hi", new Hue(0x21));

        Assert.Equal(new Hue(0x21), packet.Hue);
    }
}
