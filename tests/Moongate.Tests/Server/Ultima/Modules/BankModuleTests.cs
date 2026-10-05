using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.Bank;
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

    [Fact]
    public void Balance_IsTheGoldOfThePlayersBank_AndNilForWhoIsNotAPlayer()
    {
        _bank.Gold[_aria.Id] = 5500;

        var result = Run("return bank.balance(2), bank.balance(999), bank.balance(-1)");

        Assert.Equal(5500, result[0].Read<int>());
        Assert.Equal([LuaValue.Nil, LuaValue.Nil], result[1..]);
    }

    [Fact]
    public void Withdraw_AndDeposit_MoveTheGold_AndGiveTheAnswerOfTheBank()
    {
        _bank.Gold[_aria.Id] = 1000;

        var result = Run("return bank.withdraw(2, 300) == BankResultType.Ok, bank.deposit(2, 50) == BankResultType.Ok");

        Assert.Equal([true, true], result.Select(value => value.Read<bool>()));
        Assert.Equal([(_aria, 300)], _bank.Withdrawn);
        Assert.Equal([(_aria, 50)], _bank.Deposited);
        Assert.Equal(750, _bank.Gold[_aria.Id]);
    }

    [Fact]
    public void Withdraw_GivesTheRefusalOfTheBank()
    {
        _bank.Result = BankResultType.NotEnoughGold;

        Assert.True(Run("return bank.withdraw(2, 300) == BankResultType.NotEnoughGold")[0].Read<bool>());
        Assert.True(Run("return bank.deposit(2, 300) == BankResultType.NotEnoughGold")[0].Read<bool>());
    }

    [Theory, InlineData("999, 100"), InlineData("-1, 100")]
    public void Withdraw_AndDeposit_ForWhoIsNotAPlayer_AreNoPlayer(string arguments)
    {
        Assert.True(Run($"return bank.withdraw({arguments}) == BankResultType.NoPlayer")[0].Read<bool>());
        Assert.True(Run($"return bank.deposit({arguments}) == BankResultType.NoPlayer")[0].Read<bool>());
        Assert.Empty(_bank.Withdrawn);
        Assert.Empty(_bank.Deposited);
    }

    // Lua numbers: a fraction, or a number beyond an int, is no amount.
    [Theory, InlineData("2.5"), InlineData("99999999999"), InlineData("0/0")]
    public void Withdraw_AndDeposit_AnAmountThatIsNoWholeNumber_AreBadAmount(string amount)
    {
        Assert.True(Run($"return bank.withdraw(2, {amount}) == BankResultType.BadAmount")[0].Read<bool>());
        Assert.True(Run($"return bank.deposit(2, {amount}) == BankResultType.BadAmount")[0].Read<bool>());
        Assert.Empty(_bank.Withdrawn);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new BankModule(_bank, _mobiles));
        binder.BindEnum(state, typeof(BankResultType));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
