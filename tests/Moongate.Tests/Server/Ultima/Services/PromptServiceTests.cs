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

public sealed class PromptServiceTests : IAsyncDisposable
{
    private readonly StubPacketSendService _sender = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _aria = new()
    {
        Id = new(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0)
    };
    private readonly List<string?> _answers = [];

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private PromptService _prompts = null!;

    [Fact]
    public async Task Begin_SendsAPromptForTheCharacterWithAnIncreasingId()
    {
        await StartAsync();

        await OnLoopAsync(() => _prompts.Begin(_session, Record));
        await OnLoopAsync(() => _prompts.Begin(_session, Record));

        Assert.Equal(
            [(2u, 1), (2u, 2)],
            _sender.Sent.OfType<TextPromptPacket>().Select(prompt => (prompt.Serial.Value, prompt.PromptId))
        );
    }

    [Fact]
    public async Task Begin_AgainWhilePending_CancelsTheFirst()
    {
        await StartAsync();

        await OnLoopAsync(() => _prompts.Begin(_session, Record));
        await OnLoopAsync(() => _prompts.Begin(_session, (_, _) => { }));

        Assert.Equal([null], _answers);
    }

    [Fact]
    public async Task Begin_WithoutACharacterInTheWorld_IsCanceledAtOnceAndSendsNothing()
    {
        await StartAsync(false);

        await OnLoopAsync(() => _prompts.Begin(_session, Record));

        Assert.Equal([null], _answers);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task TryComplete_ThePendingId_GivesTheTextOnce()
    {
        await StartAsync();
        await OnLoopAsync(() => _prompts.Begin(_session, Record));
        var results = new List<bool>();

        await OnLoopAsync(
            () =>
            {
                results.Add(_prompts.TryComplete(_session, 9, "wrong"));
                results.Add(_prompts.TryComplete(_session, 1, "Vesper"));
                results.Add(_prompts.TryComplete(_session, 1, "again"));
            }
        );

        Assert.Equal([false, true, false], results);
        Assert.Equal(["Vesper"], _answers);
    }

    [Fact]
    public async Task Cancel_AndTheSessionClosing_TellTheCallbackNothingWasTyped()
    {
        await StartAsync();

        await OnLoopAsync(() => _prompts.Begin(_session, Record));
        await OnLoopAsync(() => _prompts.Cancel(_session));
        await OnLoopAsync(() => _prompts.Cancel(_session));
        await OnLoopAsync(() => _prompts.Begin(_session, Record));
        await OnLoopAsync(() => _prompts.OnSessionClosed(_session));

        Assert.Equal([null, null], _answers);
    }

    [Fact]
    public async Task ACallbackThatThrows_DoesNotBreakTheService()
    {
        await StartAsync();
        await OnLoopAsync(() => _prompts.Begin(_session, (_, _) => throw new InvalidOperationException("boom")));
        var completed = false;

        await OnLoopAsync(() => completed = _prompts.TryComplete(_session, 1, "x"));

        Assert.True(completed);
    }

    private void Record(GameSession session, string? text)
    {
        _answers.Add(text);
    }

    private async Task StartAsync(bool inWorld = true)
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, _aria.Id));

        if (inWorld)
        {
            _mobiles.EnterWorld(_aria);
        }

        _prompts = new(_mobiles, _sender);
    }

    private Task OnLoopAsync(Action action)
    {
        return _fixture.ExecuteOnLoopAsync(action);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
