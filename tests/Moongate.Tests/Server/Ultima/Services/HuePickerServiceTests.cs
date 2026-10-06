using Moongate.Core.Geometry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class HuePickerServiceTests : IAsyncDisposable
{
    private readonly StubPacketSendService _sender = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly MobileEntity _aria = new()
    {
        Id = new(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0)
    };

    private readonly List<int?> _hues = [];

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private HuePickerService _pickers = null!;

    [Fact]
    public async Task Begin_SendsThePickerWithAnIncreasingIdAndTheGraphic()
    {
        await StartAsync();

        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0E27, Record));

        Assert.Equal(
            [(1, (ushort)0x0FAB), (2, (ushort)0x0E27)],
            _sender.Sent.OfType<HuePickerPacket>().Select(packet => (packet.PickerId, packet.Graphic))
        );
    }

    [Fact]
    public async Task Begin_AgainWhileOneIsOpen_EndsTheFirstWithNoHue()
    {
        await StartAsync();

        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Ignore));

        Assert.Equal([null], _hues);
    }

    [Fact]
    public async Task Begin_WithoutACharacterInTheWorld_EndsAtOnceAndSendsNothing()
    {
        await StartAsync();
        _mobiles.LeaveWorld(_aria.Id);

        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));

        Assert.Equal([null], _hues);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task TryComplete_TheRightId_GivesTheHueOnce()
    {
        await StartAsync();
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));
        var completed = new List<bool>();

        await OnLoopAsync(() => completed.Add(_pickers.TryComplete(_session, 1, 0x0026)));
        await OnLoopAsync(() => completed.Add(_pickers.TryComplete(_session, 1, 0x0030)));

        Assert.Equal([true, false], completed);
        Assert.Equal([0x0026], _hues);
    }

    [Fact]
    public async Task TryComplete_AWrongId_KeepsThePickerOpen()
    {
        await StartAsync();
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));
        var completed = true;

        await OnLoopAsync(() => completed = _pickers.TryComplete(_session, 9, 0x0026));

        Assert.False(completed);
        Assert.Empty(_hues);
    }

    [Fact]
    public async Task TryComplete_WithNoPickerOpen_IsRefused()
    {
        await StartAsync();
        var completed = true;

        await OnLoopAsync(() => completed = _pickers.TryComplete(_session, 1, 0x0026));

        Assert.False(completed);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(1001, 1001)]
    [InlineData(1002, 1001)]
    [InlineData(0x8005, 5)]
    [InlineData(0xFFFF, 1001)]
    public async Task TryComplete_KeepsTheHueInTheDyeableRange(int picked, int expected)
    {
        await StartAsync();
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));

        await OnLoopAsync(() => _pickers.TryComplete(_session, 1, picked));

        Assert.Equal([expected], _hues);
    }

    [Fact]
    public async Task OnSessionClosed_EndsThePickerWithNoHue()
    {
        await StartAsync();
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));

        await OnLoopAsync(() => _pickers.OnSessionClosed(_session));
        await OnLoopAsync(() => _pickers.TryComplete(_session, 1, 0x0026));

        Assert.Equal([null], _hues);
    }

    [Fact]
    public async Task ACallbackThatThrows_DoesNotBreakTheLoop()
    {
        await StartAsync();
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, (_, _) => throw new InvalidOperationException("boom")));

        await OnLoopAsync(() => _pickers.TryComplete(_session, 1, 0x0026));
        await OnLoopAsync(() => _pickers.Begin(_session, 0x0FAB, Record));
        await OnLoopAsync(() => _pickers.TryComplete(_session, 2, 0x0026));

        Assert.Equal([0x0026], _hues);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private void Record(GameSession session, int? hue)
    {
        _hues.Add(hue);
    }

    private static void Ignore(GameSession session, int? hue)
    {
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, _aria.Id));
        _mobiles.EnterWorld(_aria);
        _pickers = new(_mobiles, _sender);
    }

    private Task OnLoopAsync(Action action)
    {
        return _fixture.ExecuteOnLoopAsync(action);
    }
}
