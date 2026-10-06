using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Skills;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Skills;

public sealed class SkillLockPacketHandlerTests : IAsyncLifetime
{
    private readonly RecordingMobileStateService _state = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private SkillLockPacketHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _handler = new(_fixture.Mobiles, _state);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Theory]
    [InlineData(21, 0, SkillLockType.Up)]
    [InlineData(21, 1, SkillLockType.Down)]
    [InlineData(0, 2, SkillLockType.Locked)]
    [InlineData(57, 1, SkillLockType.Down)]
    public void Handle_GivesTheLockOfTheSkillToTheCharacter_WhichChecksThem(
        int skill, byte lockValue, SkillLockType expected
    )
    {
        Handle(skill, lockValue);

        Assert.Equal([(_aria, (SkillType)skill, expected)], _state.LocksSet);
    }

    [Fact]
    public async Task Handle_ASessionWithoutACharacterInTheWorld_ChangesNothing()
    {
        var lobby = await _fixture.AddAsync(3, false);

        _handler.Handle(lobby, new() { Skill = 21, Lock = 1 });

        Assert.Empty(_state.LocksSet);
    }

    private void Handle(int skill, byte lockValue)
    {
        _handler.Handle(_session, new() { Skill = skill, Lock = lockValue });
    }
}
