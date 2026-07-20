using System.Net.Sockets;
using System.Text;
using DryIoc;
using Moongate.Console.Admin.Plugin.Data.Config;
using Moongate.Console.Admin.Plugin.Services.Hosting;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Console;

public class ConsoleAdminIntegrationTests
{
    private sealed class RecordingChatService : IChatService
    {
        public List<string> Broadcasts { get; } = [];

        public void Broadcast(string text, Hue? hue = null)
            => Broadcasts.Add(text);

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range) { }
    }

    private static (ConsoleServerService Service, RecordingChatService Chat) Build(SeededAccountService accounts)
    {
        var chat = new RecordingChatService();
        var registration = new CommandRegistration(
            "broadcast|bc",
            AccountLevelType.GrandMaster,
            "Sends a server-wide system message.",
            CommandSourceType.InGame | CommandSourceType.Console,
            _ => new BroadcastCommand(chat));
        var commands = new CommandService([registration], new Container(), accounts);
        var config = new MoongateConsoleConfig { Enabled = true, Address = "127.0.0.1", Port = 0, MaxSessions = 4 };
        var service = new ConsoleServerService(config, commands, accounts, new InlineMainThreadDispatcher());

        return (service, chat);
    }

    private static async Task<(StreamReader Reader, StreamWriter Writer, TcpClient Client)> ConnectAsync(int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        return (reader, writer, client);
    }

    /// <summary>Reads lines until one contains <paramref name="needle" />, or throws on timeout/EOF.</summary>
    private static async Task<string> ReadUntilAsync(StreamReader reader, string needle)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (true)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            Assert.NotNull(line); // EOF before the expected output

            if (line!.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }
    }

    private static async Task LoginAsync(StreamReader reader, StreamWriter writer, string user, string password)
    {
        await ReadUntilAsync(reader, "login:");
        await writer.WriteLineAsync(user);
        await ReadUntilAsync(reader, "password:");
        await writer.WriteLineAsync(password);
    }

    [Fact]
    public async Task GmLogsInAndBroadcasts()
    {
        var accounts = new SeededAccountService();
        accounts.Seed("gm", "secret", AccountLevelType.GrandMaster);
        var (service, chat) = Build(accounts);
        await service.StartAsync();

        try
        {
            var (reader, writer, client) = await ConnectAsync(service.BoundPort);
            using (client)
            {
                await LoginAsync(reader, writer, "gm", "secret");
                await writer.WriteLineAsync("broadcast hello world");

                await ReadUntilAsync(reader, "Broadcast sent.");
                Assert.Equal("hello world", Assert.Single(chat.Broadcasts));
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task PlayerLevelIsRejected()
    {
        var accounts = new SeededAccountService();
        accounts.Seed("player", "secret", AccountLevelType.Player);
        var (service, _) = Build(accounts);
        await service.StartAsync();

        try
        {
            var (reader, writer, client) = await ConnectAsync(service.BoundPort);
            using (client)
            {
                await LoginAsync(reader, writer, "player", "secret");
                await ReadUntilAsync(reader, "Insufficient privileges.");
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task WrongPasswordIsRejected()
    {
        var accounts = new SeededAccountService();
        accounts.Seed("gm", "secret", AccountLevelType.GrandMaster);
        var (service, _) = Build(accounts);
        await service.StartAsync();

        try
        {
            var (reader, writer, client) = await ConnectAsync(service.BoundPort);
            using (client)
            {
                await LoginAsync(reader, writer, "gm", "wrong");
                await ReadUntilAsync(reader, "Login failed.");
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task UnknownCommandRepliesUnknown()
    {
        var accounts = new SeededAccountService();
        accounts.Seed("gm", "secret", AccountLevelType.GrandMaster);
        var (service, _) = Build(accounts);
        await service.StartAsync();

        try
        {
            var (reader, writer, client) = await ConnectAsync(service.BoundPort);
            using (client)
            {
                await LoginAsync(reader, writer, "gm", "secret");
                await writer.WriteLineAsync("frobnicate");
                await ReadUntilAsync(reader, "Unknown command.");
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task HelpListsBroadcast()
    {
        var accounts = new SeededAccountService();
        accounts.Seed("gm", "secret", AccountLevelType.GrandMaster);
        var (service, _) = Build(accounts);
        await service.StartAsync();

        try
        {
            var (reader, writer, client) = await ConnectAsync(service.BoundPort);
            using (client)
            {
                await LoginAsync(reader, writer, "gm", "secret");
                await writer.WriteLineAsync("help");
                await ReadUntilAsync(reader, "broadcast");
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }
}
