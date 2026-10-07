using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class JailModuleTests : IAsyncLifetime
{
    private readonly StubJailService _jail = new();
    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        _jail.Now = _clock.Now.ToUnixTimeMilliseconds();
        _jail.CellList.Add(new() { Number = 1, Location = new Point3D(5276, 1164, 0) });
        _jail.CellList.Add(new() { Number = 2, Location = new Point3D(5286, 1164, 0) });
    }

    [Fact]
    public void Cells_ListsFreeAndOccupiedCells()
    {
        _jail.SentenceList.Add(Sentence(2, cell: 2, secondsLeft: 3600));

        var result = Run(
            """
            local cells = jail.cells()
            local free, taken = cells[1], cells[2]
            return #cells, free.number, free.x, free.y, free.z, free.prisoner, free.name, free.seconds_left,
                taken.number, taken.prisoner, taken.name, taken.seconds_left, free.map
            """
        );

        // The map of the jail, for mobile.teleport: Felucca.
        Assert.Equal((int)MapType.Felucca, result[12].Read<int>());

        Assert.Equal([2, 1, 5276, 1164, 0], result[..5].Select(value => value.Read<int>()));
        Assert.All(result[5..8], value => Assert.Equal(LuaValue.Nil, value));
        Assert.Equal([2, 2], result[8..10].Select(value => value.Read<int>()));
        Assert.Equal("Gino", result[10].Read<string>());
        Assert.Equal(3600, result[11].Read<int>());
    }

    [Fact]
    public void Cells_WithoutAJail_IsAnEmptyTable()
    {
        _jail.CellList.Clear();

        Assert.Equal(0, Run("return #jail.cells()")[0].Read<int>());
    }

    [Fact]
    public void Sentence_OfAPrisoner_GivesItsCellDaysTimeLeftAndWhoJailed()
    {
        _jail.SentenceList.Add(Sentence(2, cell: 2, secondsLeft: 90));

        var result = Run("local s = jail.sentence(2) return s.cell, s.days, s.seconds_left, s.by");

        Assert.Equal([2, 3, 90], result[..3].Select(value => value.Read<int>()));
        Assert.Equal("Giachi", result[3].Read<string>());
    }

    [Fact]
    public void Sentence_ThatIsOver_HasNoTimeLeft()
    {
        _jail.SentenceList.Add(Sentence(2, cell: 2, secondsLeft: -50));

        Assert.Equal(0, Run("return jail.sentence(2).seconds_left")[0].Read<int>());
    }

    [Fact]
    public void Sentence_OfSomeoneFree_IsNil()
    {
        Assert.Equal(LuaValue.Nil, Run("return jail.sentence(2)")[0]);
    }

    [Fact]
    public void Send_JailsAndAnswersOk()
    {
        var result = Run("return jail.send(2, 1, 3, 3) == JailResultType.Ok");

        Assert.True(result[0].Read<bool>());
        var (prisoner, cell, days, by) = Assert.Single(_jail.Jailed);
        Assert.Equal((new Serial(2), 1, 3, new Serial(3)), (prisoner.Id, cell, days, by.Id));
    }

    [Fact]
    public void Send_PassesTheReason_AndWithoutOneThereIsNone()
    {
        Run("jail.send(2, 1, 3, 3, 'Stole a horse') jail.send(2, 1, 3, 3)");

        Assert.Equal(["Stole a horse", null], _jail.Reasons);
    }

    [Fact]
    public void Sentence_AndAnOccupiedCell_GiveTheReason()
    {
        var sentence = Sentence(2, cell: 2, secondsLeft: 3600);
        sentence.Reason = "Stole a horse";
        _jail.SentenceList.Add(sentence);

        var result = Run("return jail.sentence(2).reason, jail.cells()[2].reason, jail.cells()[1].reason");

        Assert.Equal(("Stole a horse", "Stole a horse"), (result[0].Read<string>(), result[1].Read<string>()));
        Assert.Equal(LuaValue.Nil, result[2]);
    }

    [Fact]
    public void Send_GivesTheAnswerOfTheJail()
    {
        _jail.Result = JailResultType.CellOccupied;

        Assert.True(Run("return jail.send(2, 1, 3, 3) == JailResultType.CellOccupied")[0].Read<bool>());
    }

    [Theory, InlineData("2.5"), InlineData("0.4"), InlineData("999999999999"), InlineData("-1.5")]
    public void Send_DaysThatAreNotAWholeNumber_AreBadDays_AndNobodyIsJailed(string days)
    {
        Assert.True(Run($"return jail.send(2, 1, {days}, 3) == JailResultType.BadDays")[0].Read<bool>());
        Assert.Empty(_jail.Jailed);
    }

    [Fact]
    public void Send_ACellThatIsNotAWholeNumber_IsNoSuchCell()
    {
        Assert.True(Run("return jail.send(2, 1.5, 3, 3) == JailResultType.NoSuchCell")[0].Read<bool>());
        Assert.Empty(_jail.Jailed);
    }

    [Theory, InlineData("99, 1, 3, 3"), InlineData("2, 1, 3, 99"), InlineData("-1, 1, 3, 3")]
    public void Send_AnUnknownSerial_IsNotInWorld(string arguments)
    {
        Assert.True(Run($"return jail.send({arguments}) == JailResultType.NotInWorld")[0].Read<bool>());
        Assert.Empty(_jail.Jailed);
    }

    [Fact]
    public void Send_ASerialNotInTheWorld_GoesToTheOfflineJail_AndGivesItsAnswer()
    {
        _jail.OfflineResult = JailResultType.Pending;

        var result = Run("return jail.send(200, 1, 3, 3, 'Stole a horse') == JailResultType.Pending");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(
            (new Serial(200), 1, 3, new Serial(3)),
            Assert.Single(_jail.JailedOffline) switch { var sent => (sent.Prisoner, sent.Cell, sent.Days, sent.By.Id) }
        );
        Assert.Equal(["Stole a horse"], _jail.Reasons);
        Assert.Empty(_jail.Jailed);
    }

    [Theory, InlineData("200, 1, 3, 99"), InlineData("-1, 1, 3, 3"), InlineData("0, 1, 3, 3"),
     InlineData("99999999999, 1, 3, 3")]
    public void Send_ByNotInTheWorld_OrASerialThatIsNone_NeverReachesTheOfflineJail(string arguments)
    {
        _jail.OfflineResult = JailResultType.Pending;

        Assert.True(Run($"return jail.send({arguments}) == JailResultType.NotInWorld")[0].Read<bool>());
        Assert.Empty(_jail.JailedOffline);
    }

    [Theory, InlineData("200, 1.5, 3, 3", "NoSuchCell"), InlineData("200, 1, 2.5, 3", "BadDays")]
    public void Send_ToSomeoneOffline_StillChecksTheNumbers(string arguments, string answer)
    {
        _jail.OfflineResult = JailResultType.Pending;

        Assert.True(Run($"return jail.send({arguments}) == JailResultType.{answer}")[0].Read<bool>());
        Assert.Empty(_jail.JailedOffline);
    }

    [Fact]
    public void Sentence_OfOneThatWaits_SaysPending_AndItsWholeLength()
    {
        _jail.SentenceList.Add(
            new() { Id = new Serial(2), Name = "Gino", Cell = 2, Days = 3, JailedBy = "Giachi", Pending = true }
        );

        var result = Run(
            "local s = jail.sentence(2) return s.pending, s.seconds_left, jail.cells()[2].pending, jail.cells()[2].seconds_left"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal(3 * 86_400, result[1].Read<int>());
        Assert.True(result[2].Read<bool>());
        Assert.Equal(3 * 86_400, result[3].Read<int>());
    }

    [Fact]
    public void Sentence_OfOneThatRuns_IsNotPending()
    {
        _jail.SentenceList.Add(Sentence(2, cell: 2, secondsLeft: 90));

        var result = Run("return jail.sentence(2).pending, jail.cells()[2].pending");

        Assert.Equal([false, false], result.Select(value => value.Read<bool>()));
    }

    [Fact]
    public void Release_PardonsAPrisoner_AndIsFalseForSomeoneFree()
    {
        _jail.SentenceList.Add(Sentence(2, cell: 2, secondsLeft: 90));

        var result = Run("return jail.release(2), jail.release(3), jail.release(-1)");

        Assert.Equal([true, false, false], result.Select(value => value.Read<bool>()));
        Assert.Equal([new Serial(2), new Serial(3)], _jail.Pardoned);
    }

    [Fact]
    public void MaxDays_IsTheConfiguredOne()
    {
        _jail.MaxDays = 12;

        Assert.Equal(12, Run("return jail.max_days()")[0].Read<int>());
    }

    private JailSentenceEntity Sentence(uint prisoner, int cell, long secondsLeft)
    {
        return new()
        {
            Id = new Serial(prisoner), Name = "Gino", Cell = cell, Days = 3, JailedBy = "Giachi",
            ReleaseAt = _jail.Now + secondsLeft * 1000
        };
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new JailModule(_jail, _fixture.Mobiles, _clock));
        binder.BindEnum(state, typeof(JailResultType));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
