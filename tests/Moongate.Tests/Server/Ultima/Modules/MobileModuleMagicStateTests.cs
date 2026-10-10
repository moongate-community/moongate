using Lua;
using Lua.Standard;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MobileModuleMagicStateTests
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly SettableClock _time = new();
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca, Body = 401
    };

    public MobileModuleMagicStateTests()
    {
        _mobiles.EnterWorld(_aria);
    }

    [Fact]
    public void Paralyze_FreezesForSeconds_AndTellsIt_ThenReleaseFrees()
    {
        var result = Run(
            "return mobile.paralyze(2, 12), mobile.is_paralyzed(2), mobile.paralyze(2, 5), mobile.release_paralysis(2), mobile.is_paralyzed(2), mobile.release_paralysis(2), mobile.paralyze(999, 5), mobile.paralyze(2, 0)"
        );

        Assert.Equal([true, true, false, true, false, false, false, false], result.Select(value => value.Read<bool>()));
        Assert.False(_aria.Frozen);
    }

    [Fact]
    public void SetFrozen_OfAParalyzedMobile_TakesTheFreezeOver_TheParalysisNoLongerFreesIt()
    {
        var result = Run(
            "return mobile.paralyze(2, 12), mobile.set_frozen(2, true), mobile.is_paralyzed(2), mobile.release_paralysis(2)"
        );

        Assert.Equal([true, true, false, false], result.Select(value => value.Read<bool>()));
        Assert.True(_aria.Frozen);
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void SetFrozen_ToFreeAParalyzedMobile_EndsTheParalysis_ASecondFreezeIsNotUndoneByIt()
    {
        var result = Run(
            "return mobile.paralyze(2, 12), mobile.set_frozen(2, false), mobile.set_frozen(2, true), mobile.is_paralyzed(2)"
        );

        Assert.Equal([true, true, true, false], result.Select(value => value.Read<bool>()));
        Assert.True(_aria.Frozen);
        Assert.False(_aria.TryGetProp<long>(ParalysisService.UntilProp, out _));
    }

    [Fact]
    public void Disguise_ChangesTheNameBodyAndHue_AndEndDisguiseGivesThemBack()
    {
        var result = Run(
            "return mobile.disguise(2, { name = 'Grog', body = 400, hue = 0x3F0 }, 60), mobile.is_disguised(2), mobile.name(2), mobile.disguise(2, { name = 'X' }, 60), mobile.end_disguise(2), mobile.is_disguised(2), mobile.name(2), mobile.end_disguise(2)"
        );

        Assert.Equal(
            [true, true, "Grog", false, true, false, "Aria", false],
            result.Select(value => value.Type == LuaValueType.String ? (object)value.Read<string>() : value.Read<bool>())
        );
        Assert.Equal(401, _aria.Body);
    }

    [Fact]
    public void Disguise_WithANameList_PicksARandomNameOfTheList()
    {
        var result = Run("return mobile.disguise(2, { name_list = 'male' }, 60), mobile.name(2)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal("Alaric", result[1].Read<string>());
    }

    [Fact]
    public void Disguise_OfNothingAtAll_OrABadValue_IsRefused()
    {
        var result = Run(
            "return mobile.disguise(2, {}, 60), mobile.disguise(2, { body = -5 }, 60), mobile.disguise(2, { name_list = 'nope' }, 60), mobile.disguise(2, { name = 'X' }, 0), mobile.disguise(999, { name = 'X' }, 60)"
        );

        Assert.All(result, value => Assert.False(value.Read<bool>()));
        Assert.Equal("Aria", _aria.Name);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(
            state,
            new MobileModule(
                _mobiles,
                new RecordingTeleportService(),
                new RecordingSpeechService(),
                state: _state,
                paralysis: new ParalysisService(_state, _timers, _time),
                disguise: new DisguiseService(_state, _timers, _time),
                names: new NameService(
                    new StubDataLoaderService().With(new NameList { Id = "male", Names = ["Alaric"] })
                )
            )
        );

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
