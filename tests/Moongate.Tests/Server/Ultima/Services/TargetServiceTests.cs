using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TargetServiceTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly StubPacketSendService _sender = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _aria = new()
    {
        Id = new(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0)
    };
    private readonly List<TargetResult> _results = [];

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private TargetService _targets = null!;

    [Fact]
    public async Task Begin_SendsACursorWithAnIncreasingId()
    {
        await StartAsync();

        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));
        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Object, TargetFlagsType.Harmful, Record));

        var cursors = _sender.Sent.OfType<TargetCursorPacket>().ToList();
        Assert.Equal(
            [(1, TargetCursorType.Location), (2, TargetCursorType.Object)],
            cursors.Select(cursor => (cursor.CursorId, cursor.Cursor))
        );
    }

    [Fact]
    public async Task Begin_AgainWhilePending_CancelsTheFirstAsOverridden()
    {
        await StartAsync();

        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));
        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Ignore));

        Assert.Equal(TargetCancelType.Overridden, Assert.Single(_results).CancelReason);
    }

    [Fact]
    public async Task Cancel_SendsTheCancelAndTellsTheCallback()
    {
        await StartAsync();
        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));

        await OnLoopAsync(() => _targets.Cancel(_session));

        Assert.Equal(TargetFlagsType.Cancel, _sender.Sent.OfType<TargetCursorPacket>().Last().Flags);
        var result = Assert.Single(_results);
        Assert.Equal((TargetResultType.Canceled, TargetCancelType.Canceled), (result.Kind, result.CancelReason));
    }

    [Fact]
    public async Task OnSessionClosed_TellsTheCallbackItDisconnected()
    {
        await StartAsync();
        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));

        await OnLoopAsync(() => _targets.OnSessionClosed(_session));

        Assert.Equal(TargetCancelType.Disconnected, Assert.Single(_results).CancelReason);
    }

    [Fact]
    public async Task TryComplete_TheRightId_CallsTheCallbackOnce()
    {
        await StartAsync();
        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));
        var result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 2, 3));
        var completed = new List<bool>();

        await OnLoopAsync(() => completed.Add(_targets.TryComplete(_session, 1, result)));
        await OnLoopAsync(() => completed.Add(_targets.TryComplete(_session, 1, result)));

        Assert.Equal([true, false], completed);
        Assert.Same(result, Assert.Single(_results));
    }

    [Fact]
    public async Task TryComplete_AWrongId_KeepsTheTargetPending()
    {
        await StartAsync();
        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));
        var completed = true;

        await OnLoopAsync(
            () => completed = _targets.TryComplete(_session, 9, TargetResult.Canceled(TargetCancelType.Canceled))
        );

        Assert.False(completed);
        Assert.Empty(_results);
    }

    [Fact]
    public async Task Begin_WithoutACharacterInTheWorld_CancelsAtOnce()
    {
        await StartAsync();
        _mobiles.LeaveWorld(_aria.Id);

        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));

        Assert.Equal(TargetResultType.Canceled, Assert.Single(_results).Kind);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task RequestAsync_CompletesWithTheResult()
    {
        await StartAsync();
        var request = _targets.RequestAsync(_session, TargetCursorType.Location, TargetFlagsType.Neutral);
        await WaitForCursorAsync();
        var result = TargetResult.ForObject(new Serial(0x40000001));

        await OnLoopAsync(() => _targets.TryComplete(_session, 1, result));

        Assert.Same(result, await request.WaitAsync(Timeout));
    }

    [Fact]
    public async Task RequestAsync_TokenCancelled_CancelsItsOwnTargetOnly()
    {
        await StartAsync();
        using var cancellation = new CancellationTokenSource();
        var request = _targets.RequestAsync(
            _session,
            TargetCursorType.Location,
            TargetFlagsType.Neutral,
            cancellation.Token
        );
        await WaitForCursorAsync();

        await cancellation.CancelAsync();

        Assert.Equal(TargetResultType.Canceled, (await request.WaitAsync(Timeout)).Kind);

        await OnLoopAsync(() => _targets.Begin(_session, TargetCursorType.Location, TargetFlagsType.Neutral, Record));
        await OnLoopAsync(() => _targets.Cancel(_session));

        // The old token's cancel did not touch the newer target: it ended only through this Cancel.
        Assert.Equal(TargetCancelType.Canceled, Assert.Single(_results).CancelReason);
    }

    [Fact]
    public async Task RequestAsync_WhenAReplacedCallbackStartsAnotherTarget_TheTokenLeavesThatOneAlone()
    {
        await StartAsync();
        // The target the request replaces starts a new one from its callback.
        await OnLoopAsync(
            () => _targets.Begin(
                _session,
                TargetCursorType.Location,
                TargetFlagsType.Neutral,
                (session, _) => _targets.Begin(session, TargetCursorType.Location, TargetFlagsType.Neutral, Record)
            )
        );
        using var cancellation = new CancellationTokenSource();
        var request = _targets.RequestAsync(_session, TargetCursorType.Location, TargetFlagsType.Neutral, cancellation.Token);
        Assert.Equal(TargetCancelType.Overridden, (await request.WaitAsync(Timeout)).CancelReason);

        await cancellation.CancelAsync();
        await OnLoopAsync(() => { });

        Assert.Empty(_results);
    }

    [Fact]
    public async Task RequestAsync_SessionCloses_CompletesAsDisconnected()
    {
        await StartAsync();
        var request = _targets.RequestAsync(_session, TargetCursorType.Location, TargetFlagsType.Neutral);
        await WaitForCursorAsync();

        await OnLoopAsync(() => _targets.OnSessionClosed(_session));

        Assert.Equal(TargetCancelType.Disconnected, (await request.WaitAsync(Timeout)).CancelReason);
    }

    private void Record(GameSession session, TargetResult result)
    {
        _results.Add(result);
    }

    private static void Ignore(GameSession session, TargetResult result)
    {
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, _aria.Id));
        _mobiles.EnterWorld(_aria);
        _targets = new(_mobiles, _sender, _fixture.Loop);
    }

    private Task OnLoopAsync(Action action)
    {
        return _fixture.ExecuteOnLoopAsync(action);
    }

    // Reads the sent packets on the loop, which writes them.
    private async Task WaitForCursorAsync()
    {
        var until = DateTime.UtcNow + Timeout;
        var sent = false;

        while (!sent && DateTime.UtcNow < until)
        {
            await OnLoopAsync(() => sent = _sender.Sent.OfType<TargetCursorPacket>().Any());

            if (!sent)
            {
                await Task.Delay(10);
            }
        }

        Assert.True(sent);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
