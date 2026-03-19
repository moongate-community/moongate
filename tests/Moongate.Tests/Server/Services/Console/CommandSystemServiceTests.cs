using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Commands;
using Moongate.Server.Data.Events.Console;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Lifecycle;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Modules;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Events;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Console;

public class CommandSystemServiceTests
{
    [Test]
    public async Task ExecuteCommandAsync_WhenAddUserAlreadyExists_ShouldNotCreateAccount()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService
        {
            AccountExists = true
        };
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );

        await service.ExecuteCommandAsync("add_user testuser pass123 test@example.com");

        Assert.That(accountService.CreateCalled, Is.False);
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenAddUserArgumentsAreMissing_ShouldPrintUsage()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );

        await service.ExecuteCommandAsync("add_user onlyusername");

        Assert.That(accountService.CreateCalled, Is.False);
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("Usage: add_user"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenAddUserHasDefaultLevel_ShouldCreateRegularAccount()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );

        await service.ExecuteCommandAsync("add_user testuser pass123 test@example.com");

        Assert.Multiple(
            () =>
            {
                Assert.That(accountService.CreateCalled, Is.True);
                Assert.That(accountService.CreatedUsername, Is.EqualTo("testuser"));
                Assert.That(accountService.CreatedPassword, Is.EqualTo("pass123"));
                Assert.That(accountService.CreatedEmail, Is.EqualTo("test@example.com"));
                Assert.That(accountService.CreatedAccountType, Is.EqualTo(AccountType.Regular));
                Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("User 'testuser' created"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenAddUserHasExplicitLevel_ShouldCreateAccountWithLevel()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );

        await service.ExecuteCommandAsync("add_user gmuser pass123 gm@example.com GameMaster");

        Assert.That(accountService.CreateCalled, Is.True);
        Assert.That(accountService.CreatedAccountType, Is.EqualTo(AccountType.GameMaster));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenAddUserLevelIsInvalid_ShouldPrintValidationMessage()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );

        await service.ExecuteCommandAsync("add_user testuser pass123 test@example.com SuperAdmin");

        Assert.That(accountService.CreateCalled, Is.False);
        Assert.That(consoleUiService.Lines[^1].Message, Does.Contain("Invalid account level"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenCommandSourceIsNotAllowed_ShouldSendWarningInGame()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
    public async Task ExecuteCommandAsync_WhenLuaCommandRunsInGame_ShouldExposeCharacterIdToLuaContext()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );
        var module = new CommandModule(service);
        var script = new Script();
        var closure = script.DoString(
                                """
                                return function(ctx)
                                    captured_character_id = ctx.character_id
                                end
                                """
                            )
                            .Function;

        module.Register("lua_ctx_character", closure);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00001234u
        };

        await service.ExecuteCommandAsync("lua_ctx_character", CommandSourceType.InGame, session);

        Assert.That(
            script.Globals.Get("captured_character_id").CastToNumber(),
            Is.EqualTo((double)(uint)session.CharacterId)
        );
    }

    [Test]
    public void GetAutocompleteSuggestions_WhenCommandProvidesArgumentSuggestions_ShouldReturnExpandedLines()
    {
        var gameEventBusService = new GameEventBusService();
        var consoleUiService = new CommandSystemTestConsoleUiService();
        var outgoingPacketQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var serverLifetimeService = new CommandSystemTestServerLifetimeService();
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
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
        var accountService = new CommandSystemTestAccountService();
        var service = CreateService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );
        await service.StartAsync();

        await gameEventBusService.PublishAsync(new CommandEnteredEvent("foo"));

        Assert.That(consoleUiService.Lines.Count, Is.GreaterThan(0));
        Assert.That(consoleUiService.Lines[^1].Message, Is.EqualTo("Unknown command: foo"));
    }

    private static CommandSystemService CreateService(
        IConsoleUiService consoleUiService,
        GameEventBusService gameEventBusService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IServerLifetimeService serverLifetimeService,
        IAccountService accountService
    )
    {
        var service = new CommandSystemService(
            consoleUiService,
            gameEventBusService,
            outgoingPacketQueue,
            serverLifetimeService,
            accountService
        );

        RegisterDefaultCommands(service, consoleUiService, serverLifetimeService, accountService);

        return service;
    }

    private static void RegisterDefaultCommands(
        ICommandSystemService commandSystemService,
        IConsoleUiService consoleUiService,
        IServerLifetimeService serverLifetimeService,
        IAccountService accountService
    )
    {
        var addUserCommand = new AddUserCommand(accountService);
        var exitCommand = new ExitCommand(serverLifetimeService);
        var lockCommand = new LockCommand(consoleUiService);
        var helpCommand = new HelpCommand(commandSystemService);

        commandSystemService.RegisterCommand(
            "add_user",
            addUserCommand.ExecuteCommandAsync,
            "Creates a new account: add_user <username> <password> <email> [level].",
            CommandSourceType.Console | CommandSourceType.InGame
        );
        commandSystemService.RegisterCommand(
            "exit|shutdown",
            exitCommand.ExecuteCommandAsync,
            "Requests server shutdown."
        );
        commandSystemService.RegisterCommand(
            "lock|*",
            lockCommand.ExecuteCommandAsync,
            "Locks console input. Press '*' to unlock."
        );
        commandSystemService.RegisterCommand(
            "help|?",
            helpCommand.ExecuteCommandAsync,
            "Displays available commands.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Regular
        );
    }
}
