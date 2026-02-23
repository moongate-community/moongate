using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Session;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Events;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server;

public class CommandSystemServiceTests
{
    [Test]
    public async Task ExecuteCommandAsync_WhenCommandSourceIsNotAllowed_ShouldSendWarningInGame()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        service.RegisterCommand(
            "consoleonly",
            _ => Task.CompletedTask,
            source: CommandSourceType.Console,
            minimumAccountType: AccountType.Administrator
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        await service.ExecuteCommandAsync("consoleonly", CommandSourceType.InGame, session);

        Assert.That(outgoingPacketQueue.TryDequeue(out var outbound), Is.True);
        var speechPacket = (UnicodeSpeechMessagePacket)outbound.Packet;
        Assert.That(speechPacket.Hue, Is.EqualTo(SpeechHues.Yellow));
        Assert.That(speechPacket.Text, Does.Contain("not available"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenHandlerThrowsInGame_ShouldSendErrorHueMessage()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        service.RegisterCommand(
            "broken",
            _ => throw new InvalidOperationException("boom"),
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.Regular
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        await service.ExecuteCommandAsync("broken", CommandSourceType.InGame, session);

        Assert.That(outgoingPacketQueue.TryDequeue(out var outbound), Is.True);
        var speechPacket = (UnicodeSpeechMessagePacket)outbound.Packet;
        Assert.That(speechPacket.Hue, Is.EqualTo(SpeechHues.Red));
        Assert.That(speechPacket.Text, Is.EqualTo("Command 'broken' failed. Check logs for details."));
        Assert.That(consoleUiService.Lines.Count, Is.Zero);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenInGameAccountTypeIsTooLow_ShouldSendWarningInGame()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        service.RegisterCommand(
            "adminonly",
            _ => Task.CompletedTask,
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.Administrator
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountType = AccountType.Regular
        };

        await service.ExecuteCommandAsync("adminonly", CommandSourceType.InGame, session);

        Assert.That(outgoingPacketQueue.TryDequeue(out var outbound), Is.True);
        var speechPacket = (UnicodeSpeechMessagePacket)outbound.Packet;
        Assert.That(speechPacket.Hue, Is.EqualTo(SpeechHues.Yellow));
        Assert.That(speechPacket.Text, Does.Contain("requires account type"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenInGameCommandIsUnknown_ShouldUseWarningHue()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        await service.ExecuteCommandAsync("missing-command", CommandSourceType.InGame, session);

        Assert.That(outgoingPacketQueue.TryDequeue(out var outbound), Is.True);
        Assert.That(outbound.Packet, Is.TypeOf<UnicodeSpeechMessagePacket>());
        var speechPacket = (UnicodeSpeechMessagePacket)outbound.Packet;
        Assert.That(speechPacket.Hue, Is.EqualTo(SpeechHues.Yellow));
        Assert.That(speechPacket.Text, Is.EqualTo("Unknown command: missing-command"));
        Assert.That(consoleUiService.Lines.Count, Is.Zero);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenInGameOutputContainsCrLf_ShouldSendOnePacketPerNonEmptyLine()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        service.RegisterCommand(
            "multiline",
            context =>
            {
                context.Print("one\r\ntwo\n\nthree");

                return Task.CompletedTask;
            },
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.Regular
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        await service.ExecuteCommandAsync("multiline", CommandSourceType.InGame, session);

        var messages = new List<string>();

        while (outgoingPacketQueue.TryDequeue(out var outbound))
        {
            if (outbound.Packet is UnicodeSpeechMessagePacket speechPacket)
            {
                messages.Add(speechPacket.Text);
            }
        }

        Assert.That(messages, Is.EqualTo(new[] { "one", "two", "three" }));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSourceIsConsole_ShouldExecuteAdminCommandWithoutSession()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        var executed = false;
        service.RegisterCommand(
            "adminconsole",
            _ =>
            {
                executed = true;

                return Task.CompletedTask;
            },
            source: CommandSourceType.Console,
            minimumAccountType: AccountType.Administrator
        );

        await service.ExecuteCommandAsync("adminconsole");

        Assert.That(executed, Is.True);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSourceIsInGame_ShouldSendSpeechPacketsPerOutputLine()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000010,
                Name = "TestCharacter",
                BaseBody = 0x0190
            }
        };

        await service.ExecuteCommandAsync("help", CommandSourceType.InGame, session);

        var messages = new List<string>();

        while (outgoingPacketQueue.TryDequeue(out var outbound))
        {
            if (outbound.Packet is UnicodeSpeechMessagePacket speechPacket)
            {
                messages.Add(speechPacket.Text);
            }
        }

        Assert.Multiple(
            () =>
            {
                Assert.That(messages.Count, Is.GreaterThan(0));
                Assert.That(messages[0], Is.EqualTo("Available commands:"));
                Assert.That(messages.Any(message => message.Contains("help")), Is.True);
                Assert.That(consoleUiService.Lines.Count, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void GetAutocompleteSuggestions_WhenCommandProvidesArgumentSuggestions_ShouldReturnExpandedLines()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        service.RegisterCommand(
            "tp",
            _ => Task.CompletedTask,
            source: CommandSourceType.Console | CommandSourceType.InGame,
            minimumAccountType: AccountType.Regular,
            autocompleteProvider: _ => ["Britain", "Buccaneer's Den", "Yew"]
        );

        var suggestions = service.GetAutocompleteSuggestions("tp B");

        Assert.That(suggestions, Is.EqualTo(new[] { "tp Britain", "tp Buccaneer's Den" }));
    }

    [Test]
    public void GetAutocompleteSuggestions_WhenPrefixMatchesCommands_ShouldReturnCommandMatches()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );

        var suggestions = service.GetAutocompleteSuggestions("he");

        Assert.That(suggestions, Contains.Item("help"));
    }

    [Test]
    public async Task HandleAsync_WhenExitCommandEntered_ShouldRequestShutdown()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("exit"));

        Assert.That(serverLifetimeService.IsShutdownRequested, Is.True);
    }

    [Test]
    public async Task HandleAsync_WhenHelpCommandEntered_ShouldPrintCommandList()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("help"));

        Assert.That(consoleUiService.Lines.Count, Is.GreaterThan(0));
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("Available commands:"));
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("exit"));
    }

    [Test]
    public async Task HandleAsync_WhenHelpWithKnownCommandEntered_ShouldPrintSingleCommandHelp()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("help lock"));

        Assert.That(consoleUiService.Lines.Count, Is.GreaterThan(0));
        Assert.That(consoleUiService.Lines[^1].Message, Is.EqualTo("lock: Locks console input. Press '*' to unlock."));
    }

    [Test]
    public async Task HandleAsync_WhenHelpWithUnknownCommandEntered_ShouldPrintUnknownHelpMessage()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("help doesnotexist"));

        Assert.That(consoleUiService.Lines.Count, Is.GreaterThan(0));
        Assert.That(consoleUiService.Lines[^1].Message, Is.EqualTo("No help found for: doesnotexist"));
    }

    [Test]
    public async Task HandleAsync_WhenLockCommandEntered_ShouldLockConsoleInput()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("lock"));

        Assert.That(consoleUiService.IsInputLocked, Is.True);
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("Console input is locked."));
    }

    [Test]
    public async Task HandleAsync_WhenQuestionMarkAliasEntered_ShouldPrintCommandList()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("?"));

        Assert.That(consoleUiService.Lines.Count, Is.GreaterThan(0));
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("Available commands:"));
    }

    [Test]
    public async Task HandleAsync_WhenUnknownCommandEntered_ShouldWriteUnknownCommandMessage()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("foo"));

        Assert.That(consoleUiService.Lines.Count, Is.GreaterThan(0));
        Assert.That(consoleUiService.Lines[^1].Message, Is.EqualTo("Unknown command: foo"));
    }
}
