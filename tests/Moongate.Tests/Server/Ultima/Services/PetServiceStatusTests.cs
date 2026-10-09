using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Core.Primitives;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PetServiceStatusTests : IAsyncLifetime
{
    private readonly RecordingMobileStateService _state = new();
    private BroadcastFixture _fixture = null!;
    private PetService _service = null!;
    private MobileEntity _aria = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _service = new(
            _fixture.Mobiles,
            TestItems.Create(),
            new TamingService(new StubDataLoaderService().With<TamingCreature>()),
            new PetsConfig(),
            null,
            new Lazy<Moongate.Server.Ultima.Interfaces.IMobileStateService>(() => _state),
            _fixture.Sessions
        );
    }

    [Fact]
    public void Changed_ShowsThePlayerItsStatus()
    {
        _service.Changed(_aria.Id);

        Assert.Equal(_aria, Assert.Single(_state.Statuses).Target);
    }

    [Fact]
    public void Changed_OfSomeoneNotThere_ShowsNothing()
    {
        _service.Changed(new Serial(0x999));

        Assert.Empty(_state.Statuses);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
