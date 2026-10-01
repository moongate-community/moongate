using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class BankModuleTests
{
    private readonly StubBankService _bank = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _aria = new() { Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) };

    public BankModuleTests()
    {
        _mobiles.EnterWorld(_aria);
    }

    [Fact]
    public void Open_OpensThePlayersBank()
    {
        Assert.True(Run("return bank.open(2)")[0].Read<bool>());

        Assert.Equal([_aria], _bank.Opened);
    }

    [Fact]
    public void Open_SomeoneNotInTheWorld_IsFalse()
    {
        Assert.False(Run("return bank.open(99)")[0].Read<bool>());
        Assert.Empty(_bank.Opened);
    }

    [Fact]
    public void IsOpen_TellsWhetherThePlayersBankIsOpen()
    {
        Assert.False(Run("return bank.is_open(2)")[0].Read<bool>());

        _bank.Open(_aria);

        Assert.True(Run("return bank.is_open(2)")[0].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new BankModule(_bank, _mobiles));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
