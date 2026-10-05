using Moongate.Tests.TestSupport.Ultima.Items;
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
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class BankModuleTests
{
    private readonly StubBankService _bank = new();
    private readonly SettableClock _clock = new();
    private readonly ItemService _items = TestItems.Create();
    private BankModule? _module;
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

    [Fact]
    public void Check_AsksTheBankForACheck_AndGivesItsAnswer()
    {
        Assert.True(Run("return bank.check(2, 5000) == BankResultType.Ok")[0].Read<bool>());
        Assert.Equal([(_aria, 5000)], _bank.Checks);

        _bank.Result = BankResultType.CheckTooSmall;

        Assert.True(Run("return bank.check(2, 10) == BankResultType.CheckTooSmall")[0].Read<bool>());
    }

    [Theory, InlineData("999, 5000", "NoPlayer"), InlineData("2, 2.5", "BadAmount"), InlineData("2, 99999999999", "BadAmount")]
    public void Check_ForWhoIsNotAPlayer_OrAnAmountThatIsNone_AsksNothing(string arguments, string answer)
    {
        Assert.True(Run($"return bank.check({arguments}) == BankResultType.{answer}")[0].Read<bool>());
        Assert.Empty(_bank.Checks);
    }

    [Fact]
    public void Cash_CashesTheCheck_AndGivesTheAnswerOfTheBank()
    {
        var check = Check(0x40000500, 5000);

        Assert.True(Run("return bank.cash(2, 0x40000500) == BankResultType.Ok")[0].Read<bool>());
        Assert.Equal((_aria, check), Assert.Single(_bank.Cashed));

        _bank.Result = BankResultType.BankFull;

        Assert.True(Run("return bank.cash(2, 0x40000500) == BankResultType.BankFull")[0].Read<bool>());
    }

    [Theory, InlineData("2, 0x4000FFFF", "NotInBank"), InlineData("2, -1", "NotInBank"), InlineData("999, 0x40000500", "NoPlayer")]
    public void Cash_AnItemThatIsNotThere_OrForWhoIsNotAPlayer_AsksNothing(string arguments, string answer)
    {
        Check(0x40000500, 5000);

        Assert.True(Run($"return bank.cash({arguments}) == BankResultType.{answer}")[0].Read<bool>());
        Assert.Empty(_bank.Cashed);
    }

    [Fact]
    public void Worth_IsWhatACheckIsWorth_AndNilForAnythingElse()
    {
        Check(0x40000500, 5000);
        _items.Add([new ItemEntity { Id = new Serial(0x40000501), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 }]);

        var result = Run("return bank.worth(0x40000500), bank.worth(0x40000501), bank.worth(0x4000FFFF), bank.worth(-1)");

        Assert.Equal(5000, result[0].Read<int>());
        Assert.Equal([LuaValue.Nil, LuaValue.Nil, LuaValue.Nil], result[1..]);
    }

    private ItemEntity Check(uint serial, long worth)
    {
        var check = new ItemEntity { Id = new Serial(serial), TemplateId = "bank_check", ItemId = 0x14F0, Amount = 1 };
        _items.Add([check]);
        _bank.Worths[check.Id] = worth;

        return check;
    }

    // Several bankers hear the same words in the same moment: the first one that asks serves the player.
    [Fact]
    public void Attend_IsTrueForTheFirstWhoAsksForAPlayer_AndAgainAMomentLater()
    {
        var result = Run("return bank.attend(2), bank.attend(2), bank.attend(999)");

        Assert.Equal([true, false, false], result.Select(value => value.Read<bool>()));

        _clock.Advance(TimeSpan.FromMilliseconds(600));

        Assert.True(Run("return bank.attend(2)")[0].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, _module ??= new BankModule(_bank, _mobiles, _clock, _items));
        binder.BindEnum(state, typeof(BankResultType));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
