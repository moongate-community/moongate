using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class BroadcastCommandTests
{
    [Fact]
    public async Task ExecuteAsync_TheReply_IsInTheServerLanguage()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var session = await fixture.AddAsync(1);
        var command = new BroadcastCommand(
            new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender),
            TestLocalization.With((30019, "Messaggio inviato a {0} giocatori."))
        );
        var context = new CommandContext("broadcast hi", "broadcast", ["hi"], CommandSourceType.Console, session);

        await command.ExecuteAsync(context);

        Assert.Equal("Messaggio inviato a 1 giocatori.", Assert.Single(context.Output).Text);
    }

    [Theory]
    [InlineData(CommandSourceType.Console)]
    [InlineData(CommandSourceType.InGame)]
    public async Task ExecuteAsync_PreservesMessageSpacingAndReportsRecipientCount(CommandSourceType source)
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var session = await fixture.AddAsync(1);
        var command = new BroadcastCommand(
            new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender)
        );
        var context = new CommandContext("  BROADCAST   Hello  世界!  ", "broadcast", ["Hello", "世界!"], source, session);

        await command.ExecuteAsync(context);

        Assert.Equal("Hello  世界!", Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text);
        Assert.Equal("Broadcast sent to 1 player(s).", Assert.Single(context.Output).Text);
    }

    [Theory]
    [InlineData("broadcast")]
    [InlineData("broadcast   ")]
    public async Task ExecuteAsync_EmptyMessageReportsUsageAndSendsNothing(string line)
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var command = new BroadcastCommand(
            new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender)
        );
        var context = new CommandContext(line, "broadcast", [], CommandSourceType.Console, null);

        await command.ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Empty(fixture.Sender.Sent);
    }
}
