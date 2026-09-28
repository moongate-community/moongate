using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Speech;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Server.Ultima.Speech;

public sealed class SpeechMessageHelperTests
{
    [Fact]
    public void CreatePlayer_CopiesSpeakerAndSpeechFields()
    {
        var speech = new SpeechRequestData(SpeechType.Regular, new Hue(0x03B2), SpeechFontType.Normal, "ENU", "hello");

        var packet = SpeechMessageHelper.CreatePlayer(new Serial(7), 0x0190, "Alice", speech);
        var bytes = PacketCodec.Encode(packet);

        Assert.Equal(Convert.FromHexString("0000000701900003B20003454E5500"), bytes.AsSpan(3, 15).ToArray());
        Assert.Equal(Convert.FromHexString("00680065006C006C006F0000"), bytes.AsSpan(48).ToArray());
    }

    [Fact]
    public async Task TrySend_SystemMessage_UsesExpectedSessionConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        var sender = new StubPacketSendService();
        var packet = SpeechMessageHelper.CreateSystem("Done", new Hue(0x03B2));

        Assert.True(SpeechMessageHelper.TrySend(sender, session, packet));
        Assert.Same(fixture.Client, sender.ExpectedConnection);
        Assert.Single(sender.Sent);
        Assert.Same(packet, sender.Sent[0]);
    }
}
