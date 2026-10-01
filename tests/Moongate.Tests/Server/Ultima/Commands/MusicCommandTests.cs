using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class MusicCommandTests : IAsyncDisposable
{
    private readonly StubMusicService _music = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private SessionFixture? _fixture;

    [Fact]
    public async Task WithoutATrack_PrintsTheMusicWhereYouStand()
    {
        var context = await RunAsync();

        Assert.Equal("Music here: britain1.", Assert.Single(context.Output).Text);
        Assert.Empty(_music.Played);
    }

    [Theory, InlineData("tavern04"), InlineData("Tavern04")]
    public async Task WithATrack_PlaysItToYou(string track)
    {
        var context = await RunAsync(track);

        Assert.Equal(MusicType.Tavern04, Assert.Single(_music.Played).Music);
        Assert.Equal("Playing tavern04.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task WithATrack_PlaysItOnTheGameLoop()
    {
        await RunAsync("tavern04");
        var loopThread = 0;
        await _fixture!.ExecuteOnLoopAsync(() => loopThread = Environment.CurrentManagedThreadId);

        Assert.Equal(loopThread, _music.PlayedOnThread);
    }

    [Theory, InlineData("no_such_track"), InlineData("tavern04 now")]
    public async Task ABadTrack_ShowsTheUsage(string arguments)
    {
        var context = await RunAsync(arguments.Split(' '));

        Assert.Equal((CommandOutputLevel.Error, "Usage: music [track]"), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Empty(_music.Played);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("music", "music", [], CommandSourceType.Console, null);

        await new MusicCommand(_music, _mobiles, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync([], TestLocalization.With((30089, "Musica qui: {0}.")));

        Assert.Equal("Musica qui: britain1.", Assert.Single(context.Output).Text);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        var context = new CommandContext(".music", "music", arguments, CommandSourceType.InGame, session);

        await new MusicCommand(_music, _mobiles, _fixture.Loop, localization).ExecuteAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
