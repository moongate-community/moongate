using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Commands;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class SaveCommandTests
{
    [Fact]
    public async Task ExecuteAsync_TheBroadcast_IsInTheServerLanguage()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var saves = new ControlledWorldSaveService();
        saves.Completion.SetResult();
        var broadcast = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var command = new SaveCommand(saves, broadcast, TestLocalization.With((30015, "Il mondo è stato salvato in {0} secondi.")));

        await command.ExecuteAsync(new CommandContext("save", "save", [], CommandSourceType.Console, null));

        Assert.StartsWith("Il mondo è stato salvato in ", Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text);
    }

    [Theory]
    [InlineData(CommandSourceType.Console)]
    [InlineData(CommandSourceType.InGame)]
    public async Task ExecuteAsync_BroadcastsOnlyAfterSaveCompletes(CommandSourceType source)
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var session = await fixture.AddAsync(1);
        var saves = new ControlledWorldSaveService();
        var broadcast = new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var command = new SaveCommand(saves, broadcast);
        var context = new CommandContext("save", "save", [], source, source == CommandSourceType.InGame ? session : null);

        var saving = command.ExecuteAsync(context);

        Assert.False(saving.IsCompleted);
        Assert.Equal(1, saves.Calls);
        Assert.Empty(fixture.Sender.Sent);
        saves.Completion.SetResult();
        await saving.WaitAsync(TimeSpan.FromSeconds(5));

        var packet = Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent));
        Assert.Matches(@"^The world has been saved in \d+\.\d\d seconds\.$", packet.Text);

        if (source == CommandSourceType.Console)
        {
            Assert.Equal(packet.Text, Assert.Single(context.Output).Text);
        }
        else
        {
            Assert.Empty(context.Output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FailedSaveNeverBroadcastsSuccess()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var saves = new ControlledWorldSaveService();
        saves.Completion.SetException(new IOException("Database unavailable"));
        var command = new SaveCommand(saves,
            new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender));
        var context = new CommandContext("save", "save", [], CommandSourceType.Console, null);

        await Assert.ThrowsAsync<IOException>(() => command.ExecuteAsync(context));

        Assert.Empty(fixture.Sender.Sent);
        Assert.Empty(context.Output);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedArgumentsDoesNotSave()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var saves = new ControlledWorldSaveService();
        var command = new SaveCommand(saves,
            new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender));
        var context = new CommandContext("save now", "save", ["now"], CommandSourceType.Console, null);

        await command.ExecuteAsync(context);

        Assert.Equal(0, saves.Calls);
        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_CanceledWaitDoesNotBroadcast()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var saves = new ControlledWorldSaveService();
        var command = new SaveCommand(saves,
            new BroadcastService(fixture.Network.Loop, fixture.Sessions, fixture.Mobiles, fixture.Sender));
        using var cancellation = new CancellationTokenSource();
        var context = new CommandContext("save", "save", [], CommandSourceType.Console, null, cancellation.Token);

        var saving = command.ExecuteAsync(context);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => saving);
        Assert.Empty(fixture.Sender.Sent);
    }
}
