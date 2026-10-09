using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class HarvestModuleTests
{
    private readonly HarvestService _harvest = new(
        new StubDataLoaderService().With(new HarvestResource
            { Id = "fish", Area = 8, AmountMin = 2, AmountMax = 2, RespawnMinMinutes = 10, RespawnMaxMinutes = 10 }),
        new ManualTimeProvider(),
        new ScriptedRandom()
    );

    [Fact]
    public void TakeAndAmount_AskTheAreaOfTheCell()
    {
        var result = Run(
            """
            return harvest.amount("fish", "Trammel", 1600, 1600),
                   harvest.take("fish", "Trammel", 1600, 1600),
                   harvest.take("fish", "Trammel", 1601, 1601),
                   harvest.take("fish", "Trammel", 1602, 1602),
                   harvest.amount("fish", "Trammel", 1600, 1600)
            """
        );

        Assert.Equal(2, result[0].Read<int>());
        Assert.Equal([true, true, false], result[1..4].Select(value => value.Read<bool>()));
        Assert.Equal(0, result[4].Read<int>());
    }

    [Fact]
    public void AnUnknownResource_HasNoAmount_AndIsNotTaken()
    {
        var result = Run(
            """return harvest.has("fish"), harvest.has("gold"), harvest.amount("gold", "Trammel", 1, 1), harvest.take("gold", "Trammel", 1, 1)"""
        );

        Assert.Equal((true, false), (result[0].Read<bool>(), result[1].Read<bool>()));
        Assert.Equal(LuaValue.Nil, result[2]);
        Assert.False(result[3].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new HarvestModule(_harvest));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
