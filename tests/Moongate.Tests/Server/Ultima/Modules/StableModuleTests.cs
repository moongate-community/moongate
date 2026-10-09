using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Stable;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Stable;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class StableModuleTests
{
    private readonly RecordingStableService _stable = new();
    private readonly SettableClock _time = new();
    private StableModule? _module;
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca,
        Location = new Point3D(1600, 1600, 0)
    };

    private readonly MobileEntity _horse = new()
    {
        Id = new Serial(0x100), Name = "a horse", TemplateId = "horse", Map = MapType.Felucca,
        Location = new Point3D(1601, 1600, 0)
    };

    public StableModuleTests()
    {
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_horse);
    }

    [Fact]
    public void Pets_ListsTheStabledPetsFromOne_WithTheNameOfTheirTemplate()
    {
        _stable.Pets.AddRange(["horse", "vanished"]);

        var result = Run(
            "local pets = stable.pets(2) return #pets, pets[1].template, pets[1].name, pets[2].template, pets[2].name"
        );

        Assert.Equal(2, (int)result[0].Read<double>());
        Assert.Equal(["horse", "a horse", "vanished", "vanished"], result.Skip(1).Select(value => value.Read<string>()));
    }

    [Fact]
    public void Pets_ForAnNpcOrNobody_IsNil_AndForAPlayerWithNoPetsIsEmpty()
    {
        var result = Run("return stable.pets(0x100), stable.pets(0x999), #stable.pets(2)");

        Assert.Equal([LuaValue.Nil, LuaValue.Nil], result.Take(2));
        Assert.Equal(0, (int)result[2].Read<double>());
    }

    [Fact]
    public void Stable_CallsTheServiceWithThePlayerAndThePet_AndAnswersItsResult()
    {
        _stable.Result = StableResultType.Full;

        var result = Run("return stable.stable(2, 0x100), StableResultType.Full");

        Assert.Equal(result[1].Read<double>(), result[0].Read<double>());
        Assert.Equal((_aria, _horse), Assert.Single(_stable.Stables));
    }

    [Fact]
    public void Stable_ANobodyOrAnUnknownPet_IsRefusedWithoutAskingTheService()
    {
        var result = Run("return stable.stable(0x999, 0x100), stable.stable(2, 0x999)");

        Assert.Equal(
            [(double)StableResultType.NoPlayer, (double)StableResultType.NotAPet],
            result.Select(value => value.Read<double>())
        );
        Assert.Empty(_stable.Stables);
    }

    [Fact]
    public void Claim_TakesThePlaceFromOne_ToTheZeroBasedIndexOfTheService()
    {
        var result = Run("return stable.claim(2, 3, 'horse')");

        Assert.Equal((double)StableResultType.Ok, result[0].Read<double>());
        Assert.Equal((_aria, 2, "horse"), Assert.Single(_stable.Claims));
    }

    [Fact]
    public void Claim_ANobodyIsNoPlayer()
    {
        var result = Run("return stable.claim(0x999, 1, 'horse')");

        Assert.Equal((double)StableResultType.NoPlayer, result[0].Read<double>());
        Assert.Empty(_stable.Claims);
    }

    [Fact]
    public void Attend_TheFirstThatAsksServes_TheOthersInTheSameMomentDoNot_AndAnNpcIsNeverServed()
    {
        var result = Run("return stable.attend(2), stable.attend(2), stable.attend(0x100), stable.attend(0x999)");

        Assert.Equal([true, false, false, false], result.Select(value => value.Read<bool>()));

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.True(Run("return stable.attend(2)")[0].Read<bool>());
    }

    [Fact]
    public void MaxPetsAndFee_AreTheOnesOfTheConfig()
    {
        var result = Run("return stable.max_pets(), stable.fee()");

        Assert.Equal([10, 30], result.Select(value => (int)value.Read<double>()));
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(
            state,
            _module ??= new StableModule(
                _stable,
                _mobiles,
                new MobileTemplateService(
                    new StubDataLoaderService().With(new MobileTemplate { Id = "horse", Name = "a horse" })
                ),
                new StableConfig(),
                _time
            )
        );
        state.Environment["StableResultType"] = new LuaTable { ["Ok"] = (double)StableResultType.Ok, ["Full"] = (double)StableResultType.Full };

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
