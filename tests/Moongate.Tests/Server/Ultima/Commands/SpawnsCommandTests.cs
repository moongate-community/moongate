using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Spawns;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Spawns;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class SpawnsCommandTests : IAsyncDisposable
{
    private readonly StubSpawnRegionService _spawns = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private SessionFixture? _fixture;

    [Fact]
    public async Task PrintsTheRegionsWhereYouStand()
    {
        _spawns.Here.Add(new("felucca_0", "The Hammer And Anvil", 1, 2, TimeSpan.FromSeconds(61), false));
        _spawns.Here.Add(new("felucca_7", null, 0, 5, TimeSpan.Zero, false));
        _spawns.Here.Add(new("felucca_9", "Deep Sea", 0, 3, TimeSpan.FromSeconds(40), true));

        var context = await RunAsync();

        Assert.Equal([(MapType.Trammel, 1600, 1601)], _spawns.Asked);
        Assert.Equal(
            [
                "The Hammer And Anvil (felucca_0): 1/2 NPCs, next spawn in 2 min.",
                "felucca_7 (felucca_7): 0/5 NPCs, next spawn in 0 min.",
                "Deep Sea (felucca_9): 0/3 NPCs, no spot found, retrying in 1 min."
            ],
            context.Output.Select(line => line.Text)
        );
    }

    [Fact]
    public async Task NoRegionHere_SaysSo()
    {
        var context = await RunAsync();

        Assert.Equal("No spawn region here.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(TestLocalization.With((30078, "Nessuna regione di spawn qui.")));

        Assert.Equal("Nessuna regione di spawn qui.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("spawns", "spawns", [], CommandSourceType.Console, null);

        await new SpawnsCommand(_spawns, _mobiles).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Empty(_spawns.Asked);
    }

    private async Task<CommandContext> RunAsync(ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        _mobiles.EnterWorld(new MobileEntity { Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1600, 1601, 0) });
        var context = new CommandContext(".spawns", "spawns", [], CommandSourceType.InGame, session);

        await new SpawnsCommand(_spawns, _mobiles, localization).ExecuteAsync(context);

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
