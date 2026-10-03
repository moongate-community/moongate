using System.Buffers.Binary;
using System.Text;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Commands;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Commands;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

public sealed class SpeechHandlerFixture : IAsyncDisposable
{
    private readonly Container _container = new();
    private readonly List<ControlledNetworkConnection> _connections = [];
    private readonly SessionFixture _network;

    public GameSession Speaker { get; }
    public SessionService Sessions { get; }
    public MobileService Mobiles { get; }
    public StubPacketSendService Sender { get; } = new();
    public CommandSystemService Commands { get; }
    public RecordingCommandExecutor AccountExecutor => _container.Resolve<RecordingCommandExecutor>();
    public DelayedCommandExecutor DelayedExecutor => _container.Resolve<DelayedCommandExecutor>();
    public SpeechRequestPacketHandler Handler { get; }
    public RecordingNpcSpeechListener Listener { get; } = new();

    public RecordingItemSpeechListener ItemListener { get; } = new();

    /// <summary>
    ///     Gets what the handler published on the event bus for the speech of players.
    /// </summary>
    public List<PlayerSaidEvent> Said { get; } = [];

    private SpeechHandlerFixture(SessionFixture network, ILogger? commandLogger, ILocalizationService? localization)
    {
        _network = network;
        Sessions = new(network.Loop);
        Speaker = Sessions.GetOrCreate(network.Client);
        Mobiles = new(new StubMovementService(), TestSectors.Create());
        _container.RegisterCommand<HelpCommand>(
            "help",
            source: CommandSourceType.Console | CommandSourceType.InGame,
            minimumAccountType: AccountType.Regular
        );
        _container.RegisterCommand<RecordingCommandExecutor>(
            "account",
            source: CommandSourceType.Console | CommandSourceType.InGame,
            minimumAccountType: AccountType.Administrator
        );
        _container.RegisterCommand<DelayedCommandExecutor>(
            "wait",
            source: CommandSourceType.InGame,
            minimumAccountType: AccountType.Regular
        );
        Commands = commandLogger is null
            ? new(_container.Resolve<CommandRegistry>(), _container)
            : new(_container.Resolve<CommandRegistry>(), _container, commandLogger);
        _container.RegisterMoongateEventBus();
        var events = _container.Resolve<IMoongateEventBus>();
        events.Subscribe<PlayerSaidEvent>(
            (said, _) =>
            {
                Said.Add(said);

                return Task.CompletedTask;
            }
        );
        Handler = new(Commands, Sessions, Mobiles, Sender, localization, Listener, events, ItemListener);
    }

    public static async Task<SpeechHandlerFixture> CreateAsync(ILogger? commandLogger = null, ILocalizationService? localization = null)
    {
        var fixture = new SpeechHandlerFixture(await SessionFixture.CreateAsync(), commandLogger, localization);
        await fixture.Commands.StartAsync();

        return fixture;
    }

    public async Task EnterSpeakerAsync(AccountType accountType = AccountType.Regular)
    {
        await EnterAsync(Speaker, new Serial(1), "Alice", MapType.Trammel, new Point3D(100, 100, 0), accountType);
    }

    public async Task<GameSession> AddPlayerAsync(
        long sessionId,
        uint serial,
        string name,
        MapType map,
        int x,
        int y,
        AccountType accountType = AccountType.Regular
    )
    {
        var connection = new ControlledNetworkConnection(sessionId);
        _connections.Add(connection);
        var session = Sessions.GetOrCreate(connection);
        await EnterAsync(session, new Serial(serial), name, map, new Point3D(x, y, 0), accountType);

        return session;
    }

    public PacketContext Context()
    {
        return new(Speaker, _network.Loop, Sessions, Sender);
    }

    public UnicodeSpeechRequestPacket Unicode(string text, byte type = 0)
    {
        var message = Encoding.BigEndianUnicode.GetBytes(text + "\0");
        var frame = new byte[12 + message.Length];
        frame[0] = 0xAD;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1), (ushort)frame.Length);
        frame[3] = type;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), 0x03B2);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6), 3);
        "ENU"u8.CopyTo(frame.AsSpan(8));
        message.CopyTo(frame, 12);
        Assert.True(UnicodeSpeechRequestPacket.TryParse(frame, out var packet));

        return packet;
    }

    public void ReplaceSpeakerSession()
    {
        Sessions.Remove(Speaker.SessionId);
        var replacement = new ControlledNetworkConnection(Speaker.SessionId);
        _connections.Add(replacement);
        Sessions.GetOrCreate(replacement);
    }

    private async Task EnterAsync(
        GameSession session,
        Serial serial,
        string name,
        MapType map,
        Point3D location,
        AccountType accountType
    )
    {
        await _network.ExecuteOnLoopAsync(() =>
        {
            session.Set(SessionKeys.CharacterId, serial);
            session.Set(SessionKeys.AccountType, accountType);
            Mobiles.EnterWorld(new MobileEntity
            {
                Id = serial, AccountId = new Serial(42), Name = name, Map = map, Location = location, Body = 0x0190
            });
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Commands.StopAsync();
        _container.Dispose();

        foreach (var connection in _connections)
        {
            connection.Dispose();
        }

        await _network.DisposeAsync();
    }
}
