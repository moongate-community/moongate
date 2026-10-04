using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class SpeechRequestPacketHandlerTests
{
    [Fact]
    public async Task Handle_Say_ReachesSenderAndSameMapPlayersWithin15Tiles()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();
        var near = await fixture.AddPlayerAsync(1001, 2, "Near", MapType.Trammel, 115, 100);
        await fixture.AddPlayerAsync(1002, 3, "Far", MapType.Trammel, 116, 100);
        await fixture.AddPlayerAsync(1003, 4, "OtherMap", MapType.Felucca, 100, 100);

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello"), CancellationToken.None);

        Assert.Equal(2, fixture.Sender.Sent.Count);
        Assert.Contains(fixture.Speaker.SessionId, fixture.Sender.SentSessionIds);
        Assert.Contains(near.SessionId, fixture.Sender.SentSessionIds);
        Assert.All(fixture.Sender.Sent, packet =>
        {
            var message = Assert.IsType<UnicodeSpeechMessagePacket>(packet);
            Assert.Equal("hello", message.Text);
            Assert.Equal(SpeechType.Regular, message.Type);
            Assert.Equal("Alice", message.Name);
        });
    }

    [Fact]
    public async Task Handle_SayOfAHiddenSpeaker_ReachesTheSpeakerAndTheStaffOnly()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync(AccountType.GameMaster);
        var player = await fixture.AddPlayerAsync(1001, 2, "Player", MapType.Trammel, 105, 100);
        var staff = await fixture.AddPlayerAsync(1002, 3, "Staff", MapType.Trammel, 106, 100, AccountType.GameMaster);
        Assert.True(fixture.Mobiles.TryGet(fixture.Speaker.CharacterId, out var speaker));
        speaker.Hidden = true;

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello"), CancellationToken.None);

        Assert.Equal(2, fixture.Sender.Sent.Count);
        Assert.Contains(fixture.Speaker.SessionId, fixture.Sender.SentSessionIds);
        Assert.Contains(staff.SessionId, fixture.Sender.SentSessionIds);
        Assert.DoesNotContain(player.SessionId, fixture.Sender.SentSessionIds);
    }

    [Fact]
    public async Task Handle_NoWorldCharacterOrUnsupportedMode_SendsNothing()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello"), CancellationToken.None);
        await fixture.EnterSpeakerAsync();
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello", 8), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("   "), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(new string('a', 129)), CancellationToken.None);

        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task Handle_Say_TellsTheNpcsWhatThePlayerSaid()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello"), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("..dot"), CancellationToken.None);

        Assert.Equal([("Alice", "hello"), ("Alice", ".dot")], fixture.Listener.Heard.Select(entry => (entry.Speaker.Name, entry.Text)));
    }

    [Fact]
    public async Task Handle_Say_TellsTheGuardsWhatThePlayerSaid_ButNotACommand()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("guards"), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync();

        Assert.Equal([("Alice", "guards")], fixture.Guards.Heard.Select(entry => (entry.Speaker.Name, entry.Text)));
    }

    [Fact]
    public async Task Handle_Say_TellsTheItemsWhatThePlayerSaid_ButNotACommand()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("om om om"), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync();

        Assert.Equal([("Alice", "om om om")], fixture.ItemListener.Heard.Select(entry => (entry.Speaker.Name, entry.Text)));
    }

    [Fact]
    public async Task Handle_Say_PublishesWhatThePlayerSaid()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello"), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("..dot"), CancellationToken.None);

        Assert.Equal(
            [(new Serial(1), "hello"), (new Serial(1), ".dot")],
            fixture.Said.Select(said => (said.Speaker.Id, said.Text))
        );
    }

    [Fact]
    public async Task Handle_ACommandOrASessionWithoutACharacter_PublishesNothing()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("hello"), CancellationToken.None);
        await fixture.EnterSpeakerAsync();
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync();

        Assert.Empty(fixture.Said);
    }

    [Fact]
    public async Task Handle_Say_PassesTheClientsKeywordsToTheNpcs()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();
        Assert.True(
            UnicodeSpeechRequestPacket.TryParse(Convert.FromHexString("AD00168003B20003454E5500002001002062616E6B00"), out var packet)
        );

        await fixture.Handler.HandleAsync(fixture.Context(), packet, CancellationToken.None);

        Assert.Equal([0x001, 0x002], Assert.Single(fixture.Listener.Keywords));
    }

    [Fact]
    public async Task Handle_ACommand_IsNotHeardByTheNpcs()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync();

        Assert.Empty(fixture.Listener.Heard);
    }

    [Fact]
    public async Task Handle_DoubleDot_SaysOneLiteralDot()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode("..hello"), CancellationToken.None);

        Assert.Equal(".hello", Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text);
    }

    [Fact]
    public async Task Handle_HelpAndUnknownCommand_OnlyReplyToInvoker()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();
        await fixture.AddPlayerAsync(1001, 2, "Near", MapType.Trammel, 101, 100);

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".missing"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotEmpty(fixture.Sender.Sent);
        Assert.All(fixture.Sender.SentSessionIds, sessionId => Assert.Equal(fixture.Speaker.SessionId, sessionId));
        Assert.All(fixture.Sender.Sent, packet => Assert.Equal(SpeechType.System, Assert.IsType<UnicodeSpeechMessagePacket>(packet).Type));
        Assert.Contains(fixture.Sender.Sent, packet => Assert.IsType<UnicodeSpeechMessagePacket>(packet).Text.Contains("Available commands", StringComparison.Ordinal));
        Assert.Contains(fixture.Sender.Sent, packet => Assert.IsType<UnicodeSpeechMessagePacket>(packet).Text.Contains("Unknown command", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_AccountCommand_UsesInGamePermissionsAndNeverBroadcastsPassword()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();
        await fixture.AddPlayerAsync(1001, 2, "Near", MapType.Trammel, 101, 100);

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".account create bob secret"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(fixture.AccountExecutor.Invocations);
        Assert.All(fixture.Sender.SentSessionIds, id => Assert.Equal(fixture.Speaker.SessionId, id));
        Assert.DoesNotContain(fixture.Sender.Sent, packet => Assert.IsType<UnicodeSpeechMessagePacket>(packet).Text.Contains("secret", StringComparison.Ordinal));

        await fixture.EnterSpeakerAsync(AccountType.Administrator);
        fixture.Sender.Sent.Clear();
        fixture.Sender.SentSessionIds.Clear();
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".account create bob secret"), CancellationToken.None);
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var invocation = Assert.Single(fixture.AccountExecutor.Invocations);
        Assert.Same(fixture.Speaker, invocation.Session);
        Assert.True(invocation.IsInGame);
        Assert.Equal(["create", "bob", "secret"], invocation.Arguments);
        Assert.All(fixture.Sender.SentSessionIds, id => Assert.Equal(fixture.Speaker.SessionId, id));
        Assert.Equal("ok", Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text);
    }

    [Fact]
    public async Task Handle_AccountCommandWithTabs_InvokesCommandWithoutBroadcastingPassword()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        await using var fixture = await SpeechHandlerFixture.CreateAsync(logger);
        await fixture.EnterSpeakerAsync(AccountType.Administrator);
        await fixture.AddPlayerAsync(1001, 2, "Near", MapType.Trammel, 101, 100);

        await fixture.Handler.HandleAsync(
            fixture.Context(),
            fixture.Unicode(".account\tcreate\tbob\tsecret"),
            CancellationToken.None
        );
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var invocation = Assert.Single(fixture.AccountExecutor.Invocations);
        Assert.Equal(["create", "bob", "secret"], invocation.Arguments);
        Assert.All(fixture.Sender.SentSessionIds, id => Assert.Equal(fixture.Speaker.SessionId, id));
        Assert.DoesNotContain(fixture.Sender.Sent, packet =>
            Assert.IsType<UnicodeSpeechMessagePacket>(packet).Text.Contains("secret", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Events, entry => entry.RenderMessage().Contains("secret", StringComparison.Ordinal));
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    [Fact]
    public async Task Handle_CommandReplyAfterSessionReplacement_IsDiscarded()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        var handling = fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".wait"), CancellationToken.None).AsTask();
        await fixture.DelayedExecutor.Started.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.ReplaceSpeakerSession();
        fixture.DelayedExecutor.Release();
        await handling.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task Handle_ASecondCommandWhileOneRuns_IsRefused()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".wait"), CancellationToken.None);
        await fixture.DelayedExecutor.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);

        Assert.Equal(
            "A command is already running.",
            Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text
        );
        fixture.DelayedExecutor.Release();
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ASecondCommandWhileOneRuns_IsRefusedInTheServerLanguage()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync(
            localization: TestLocalization.With((30009, "Un comando è già in esecuzione."))
        );
        await fixture.EnterSpeakerAsync();
        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".wait"), CancellationToken.None);
        await fixture.DelayedExecutor.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".help"), CancellationToken.None);

        Assert.Equal(
            "Un comando è già in esecuzione.",
            Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text
        );
        fixture.DelayedExecutor.Release();
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ASlowCommand_DoesNotHoldTheSessionAndRepliesWhenItEnds()
    {
        await using var fixture = await SpeechHandlerFixture.CreateAsync();
        await fixture.EnterSpeakerAsync();

        // A command waiting for the player (a target cursor) must not keep the session's next packets waiting.
        var handling = fixture.Handler.HandleAsync(fixture.Context(), fixture.Unicode(".wait"), CancellationToken.None).AsTask();
        await fixture.DelayedExecutor.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await handling.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(fixture.Sender.Sent);

        fixture.DelayedExecutor.Release();
        await fixture.Handler.WaitForCommandsAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("done", Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent)).Text);
    }
}
