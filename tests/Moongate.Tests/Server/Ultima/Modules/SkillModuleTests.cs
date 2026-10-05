using Lua;
using Lua.Standard;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class SkillModuleTests : IAsyncLifetime
{
    private readonly StubSkillService _skills = new();

    private BroadcastFixture _fixture = null!;
    private MobileEntity _aria = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Check_TriesTheMobileAtTheSkillNamed_AndAnswersTheTry(bool result)
    {
        _skills.Result = result;

        Assert.Equal(result, Run("return skill.check(2, 'animal_lore', 30, 80.5)")[0].Read<bool>());

        Assert.Equal([(_aria, SkillType.AnimalLore, 30.0, 80.5)], _skills.Checks);
    }

    [Theory]
    [InlineData("return skill.check(2, 'juggling', 0, 100)")]
    [InlineData("return skill.check(2, '', 0, 100)")]
    [InlineData("return skill.check(2, '9999', 0, 100)")]
    [InlineData("return skill.check(99, 'hiding', 0, 100)")]
    [InlineData("return skill.check(0, 'hiding', 0, 100)")]
    [InlineData("return skill.check(-1, 'hiding', 0, 100)")]
    public void Check_AnUnknownSkillOrAMobileNotInTheWorld_IsFalse_AndTriesNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_skills.Checks);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new SkillModule(_skills, _fixture.Mobiles));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
