using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class GumpServiceTests : IAsyncLifetime
{
    private readonly List<(GameSession Session, GumpResponse Response)> _responses = [];

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private GumpService _gumps = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(1);
        _gumps = new(_fixture.Sender);
    }

    [Fact]
    public async Task Open_SendsTheCompressedGumpToAModernClient()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        var packet = Assert.IsType<CompressedGumpPacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Equal((GumpService.TypeIdOf("confirm"), 100, 50), (packet.TypeId, packet.X, packet.Y));
        Assert.NotEqual(0u, packet.Serial);
    }

    [Fact]
    public async Task Open_SendsTheUncompressedGumpToAnOldClient()
    {
        _session.NetworkSession.SetClientVersion(new ClientVersion(4, 0, 11, 0));

        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        Assert.IsType<GumpPacket>(Assert.Single(_fixture.Sender.Sent));
    }

    [Fact]
    public void TypeIdOf_IsStable_AndNeverZeroOrTheVirtueGump()
    {
        Assert.Equal(GumpService.TypeIdOf("confirm"), GumpService.TypeIdOf("confirm"));
        Assert.NotEqual(GumpService.TypeIdOf("confirm"), GumpService.TypeIdOf("other"));
        Assert.DoesNotContain(
            Enumerable.Range(0, 5000).Select(index => GumpService.TypeIdOf($"gump{index}")),
            id => id is 0 or 0x1CD
        );
    }

    [Fact]
    public async Task AReply_ReachesTheGumpWithItsSwitchesAndTexts()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2, [10], [(3, "Fido")])));

        var response = Assert.Single(_responses).Response;
        Assert.Equal(2, response.ButtonId);
        Assert.Equal([10], response.Switches);
        Assert.Equal("Fido", response.Texts[3]);
    }

    [Fact]
    public async Task AGump_AnswersOnce()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2)));
        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2)));

        Assert.Single(_responses);
    }

    [Fact]
    public async Task Closing_IsAnAnswerWithButtonZero()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(0)));

        Assert.Equal(0, Assert.Single(_responses).Response.ButtonId);
    }

    [Theory,
     InlineData(7, 10, 3, 5),
     InlineData(2, 99, 3, 5),
     InlineData(2, 10, 99, 5),
     InlineData(2, 10, 3, 240)]
    public async Task AReplyWithWhatWasNotSent_IsDropped(int button, int switchId, int entry, int textLength)
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(button, [switchId], [(entry, new string('a', textLength))])));

        Assert.Empty(_responses);
    }

    [Fact]
    public async Task AReplyForAGumpNeverSent_IsDropped()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));
        var sent = Assert.IsType<CompressedGumpPacket>(_fixture.Sender.Sent[0]);

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2, serial: sent.Serial + 1)));
        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2, typeId: GumpService.TypeIdOf("other"))));

        Assert.Empty(_responses);
    }

    [Fact]
    public async Task OpeningTheSameGumpAgain_ClosesTheFirst()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));
        var first = Assert.IsType<CompressedGumpPacket>(_fixture.Sender.Sent[0]);

        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        var close = Assert.IsType<CloseGumpPacket>(_fixture.Sender.Sent[1]);
        Assert.Equal((first.TypeId, 0), (close.TypeId, close.ButtonId));
        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2, serial: first.Serial)));
        Assert.Empty(_responses);
    }

    [Fact]
    public async Task Close_ClosesTheGumpOnTheClient_AndItsReplyIsDropped()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        var closed = new List<bool>();
        await OnLoopAsync(() => closed.Add(_gumps.Close(_session, "confirm")));
        await OnLoopAsync(() => closed.Add(_gumps.Close(_session, "confirm")));
        Assert.Equal([true, false], closed);

        Assert.IsType<CloseGumpPacket>(_fixture.Sender.Sent[1]);
        await OnLoopAsync(() => _gumps.Respond(_session, Reply(0)));
        Assert.Empty(_responses);
    }

    [Fact]
    public async Task ACallbackThatThrows_IsLogged()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm((_, _) => throw new InvalidOperationException("boom"))));

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2)));
    }

    [Fact]
    public async Task ASession_KeepsAtMostSixtyFourGumps()
    {
        for (var index = 0; index < 65; index++)
        {
            await OnLoopAsync(() => _gumps.Open(_session, Confirm(id: $"gump{index}")));
        }

        var oldest = Assert.IsType<CompressedGumpPacket>(_fixture.Sender.Sent[0]);
        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2, serial: oldest.Serial, typeId: oldest.TypeId)));
        Assert.Empty(_responses);
    }

    [Fact]
    public async Task AClosedSession_ForgetsItsGumps()
    {
        await OnLoopAsync(() => _gumps.Open(_session, Confirm()));

        await OnLoopAsync(() => _gumps.OnSessionClosed(_session));

        await OnLoopAsync(() => _gumps.Respond(_session, Reply(2)));
        Assert.Empty(_responses);
    }

    private Task OnLoopAsync(Action action)
    {
        return _fixture.Network.ExecuteOnLoopAsync(action);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private GumpInstance Confirm(Action<GameSession, GumpResponse>? onResponse = null, string id = "confirm")
    {
        var layout = new GumpLayout()
                     .Add(new GumpBackground { GumpId = 9200, Width = 300, Height = 200 })
                     .Add(new GumpButton { Up = 4005, Down = 4007, ButtonId = 2 })
                     .Add(new GumpCheckbox { Off = 210, On = 211, SwitchId = 10 })
                     .Add(new GumpTextEntry { Width = 100, Height = 20, EntryId = 3 });

        return new()
        {
            Id = id, Layout = layout, X = 100, Y = 50,
            OnResponse = onResponse ?? ((session, response) => _responses.Add((session, response)))
        };
    }

    private GumpResponsePacket Reply(
        int button,
        int[]? switches = null,
        (int, string)[]? texts = null,
        uint? serial = null,
        uint? typeId = null
    )
    {
        var sent = _fixture.Sender.Sent.OfType<CompressedGumpPacket>().Last();

        return new()
        {
            Serial = serial ?? sent.Serial, TypeId = typeId ?? sent.TypeId, ButtonId = button, Switches = switches ?? [],
            TextEntries = texts ?? []
        };
    }
}
