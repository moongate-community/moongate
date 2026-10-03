using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class MobileQueryPacketHandlerTests : IAsyncLifetime
{
    private static readonly Serial Aria = new(2);
    private static readonly Serial Boris = new(3);

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileQueryPacketHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(Aria.Value);
        await _fixture.AddAsync(Boris.Value);
        var world = new WorldConfig();
        _handler = new(
            _fixture.Mobiles,
            new MobileStateService(_fixture.Mobiles, _fixture.Sessions, _fixture.Sectors, _fixture.Sender, new RecordingWorldViewService(), world),
            world
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Handle_TheSkills_SendsTheWholeListOfTheCharacter()
    {
        Assert.True(_fixture.Mobiles.TryGet(Aria, out var aria));
        aria.Skills.Add(new() { Skill = SkillType.Magery, Base = 505 });

        Handle(MobileQueryType.Skills, Aria);

        var skills = Assert.IsType<SkillsPacket>(Assert.Single(_fixture.Sender.Sent)).Skills;
        Assert.Equal(505, skills[(int)SkillType.Magery].Base);
    }

    [Fact]
    public void Handle_TheStatusOfTheCharacter_SendsAllOfIt()
    {
        Handle(MobileQueryType.Status, Aria);

        Assert.False(Assert.IsType<MobileStatusPacket>(Assert.Single(_fixture.Sender.Sent)).Compact);
    }

    [Fact]
    public void Handle_TheStatusOfAMobileInSight_SendsItsNameAndHealthBar()
    {
        Handle(MobileQueryType.Status, Boris);

        var status = Assert.IsType<MobileStatusPacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Equal((true, Boris), (status.Compact, status.Status.Serial));
    }

    [Fact]
    public void Handle_TheStatusOfAMobileOutOfSight_SendsNothing()
    {
        Assert.True(_fixture.Mobiles.TryGet(Boris, out var boris));
        _fixture.Mobiles.MoveTo(boris, boris.Map, new Point3D(19, 0, 0));

        Handle(MobileQueryType.Status, Boris);

        Assert.Empty(_fixture.Sender.Sent);

        _fixture.Mobiles.MoveTo(boris, boris.Map, new Point3D(18, 0, 0));
        Handle(MobileQueryType.Status, Boris);

        Assert.Single(_fixture.Sender.Sent);
    }

    [Fact]
    public void Handle_TheStatusOfAMobileOnAnotherMap_SendsNothing()
    {
        Assert.True(_fixture.Mobiles.TryGet(Boris, out var boris));
        _fixture.Mobiles.MoveTo(boris, MapType.Felucca, new Point3D(0, 0, 0));

        Handle(MobileQueryType.Status, Boris);

        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void Handle_AnUnknownMobileOrKind_SendsNothing()
    {
        Handle(MobileQueryType.Status, new Serial(0x999));
        Handle((MobileQueryType)9, Aria);

        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void Handle_WithoutACharacter_SendsNothing()
    {
        var stranger = _fixture.Sessions.GetOrCreate(new Moongate.Tests.TestSupport.Network.ControlledNetworkConnection(77));

        _handler.Handle(stranger, new MobileQueryPacket { Kind = MobileQueryType.Skills, Target = Aria });

        Assert.Empty(_fixture.Sender.Sent);
    }

    private void Handle(MobileQueryType kind, Serial target)
    {
        _handler.Handle(_session, new MobileQueryPacket { Kind = kind, Target = target });
    }
}
