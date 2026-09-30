using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Decorations;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class DecorateCommandTests : IAsyncDisposable
{
    private readonly StubPacketSendService _sender = new();
    private readonly StubDecorationService _decorations = new(
        new DecorationFileResult("britannia", "britain", 1180, 3, new Dictionary<string, int> { ["Spawner"] = 4, ["Teleporter"] = 8 }),
        new DecorationFileResult("trammel", "moonglow", 10, 0, new Dictionary<string, int>())
    );

    private SessionFixture? _fixture;

    [Fact]
    public async Task InGame_SendsALinePerFileAsItGoes_ThenPrintsTheTotals()
    {
        var context = await RunInGameAsync();

        Assert.Equal(
            [
                "Decorating britannia/britain: 1180 placed, 3 already there, 12 skipped (Teleporter 8, Spawner 4).",
                "Decorating trammel/moonglow: 10 placed, 0 already there, 0 skipped."
            ],
            _sender.Sent.OfType<UnicodeSpeechMessagePacket>().Select(packet => packet.Text)
        );
        Assert.Equal(
            "Decoration done: 1190 placed, 3 already there, 12 skipped in 2 files.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task FromTheConsole_PrintsTheTotals()
    {
        var context = new CommandContext("decorate", "decorate", [], CommandSourceType.Console, null);

        await new DecorateCommand(_decorations, _sender).ExecuteAsync(context);

        Assert.Equal("Decoration done: 1190 placed, 3 already there, 12 skipped in 2 files.", Assert.Single(context.Output).Text);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task WithArguments_ShowsTheUsage_AndPlacesNothing()
    {
        var context = new CommandContext("decorate now", "decorate", ["now"], CommandSourceType.Console, null);

        await new DecorateCommand(_decorations, _sender).ExecuteAsync(context);

        Assert.Equal((CommandOutputLevel.Error, "Usage: decorate"), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Equal(0, _decorations.Calls);
    }

    [Fact]
    public async Task AFailure_PointsToTheLogs()
    {
        var failing = new StubDecorationService { Failure = new InvalidDataException("bad folder") };
        var context = new CommandContext("decorate", "decorate", [], CommandSourceType.Console, null);

        await new DecorateCommand(failing, _sender).ExecuteAsync(context);

        Assert.Equal(
            (CommandOutputLevel.Error, "The decoration failed. Check the server logs."),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunInGameAsync(
            TestLocalization.With(
                (30055, "Decoro {0}/{1}: {2} piazzati, {3} già presenti, {4} saltati{5}."),
                (30056, "Decorazione finita: {0} piazzati, {1} già presenti, {2} saltati in {3} file.")
            )
        );

        Assert.StartsWith("Decoro britannia/britain: 1180 piazzati", _sender.Sent.OfType<UnicodeSpeechMessagePacket>().First().Text);
        Assert.StartsWith("Decorazione finita: 1190 piazzati", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunInGameAsync(ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext(".decorate", "decorate", [], CommandSourceType.InGame, session);

        await new DecorateCommand(_decorations, _sender, localization).ExecuteAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
