using Moongate.Core.Geometry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Prompts;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Prompts;

public sealed class TextPromptResponsePacketHandlerTests : IAsyncDisposable
{
    private readonly StubPacketSendService _sender = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly List<string?> _answers = [];

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private PromptService _prompts = null!;

    [Fact]
    public async Task Handle_TheAnswerToThePendingPrompt_GivesItsTextTrimmed()
    {
        await StartAsync();

        await AnswerAsync(1, 1, "  Vesper ");

        Assert.Equal(["Vesper"], _answers);
    }

    [Fact]
    public async Task Handle_AnEscape_GivesNothing()
    {
        await StartAsync();

        await AnswerAsync(1, 0, "ignored");

        Assert.Equal([null], _answers);
    }

    [Fact]
    public async Task Handle_OnlySpaces_GivesNothing_AsAnEscape()
    {
        await StartAsync();

        await AnswerAsync(1, 1, "   ");

        Assert.Equal([null], _answers);
    }

    [Fact]
    public async Task Handle_AnotherIdOrATextOver128Characters_IsIgnoredAndThePromptStaysPending()
    {
        await StartAsync();

        await AnswerAsync(5, 1, "Vesper");
        await AnswerAsync(1, 1, new string('a', 129));
        Assert.Empty(_answers);

        await AnswerAsync(1, 1, new string('a', 128));
        Assert.Equal([new string('a', 128)], _answers);
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var aria = new MobileEntity { Id = new(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0) };
        _mobiles.EnterWorld(aria);
        _prompts = new(_mobiles, _sender);
        await _fixture.ExecuteOnLoopAsync(
            () =>
            {
                _session.Set(SessionKeys.CharacterId, aria.Id);
                _prompts.Begin(_session, (_, text) => _answers.Add(text));
            }
        );
    }

    private Task AnswerAsync(int id, int type, string text)
    {
        var handler = new TextPromptResponsePacketHandler(_prompts);
        var packet = new TextPromptResponsePacket(0) { Serial = new(2), PromptId = id, IsCancel = type == 0, Text = text };

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, packet));
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
