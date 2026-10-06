using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.BulletinBoards;
using Moongate.Tests.TestSupport.Ultima.BulletinBoards;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class BoardModuleTests : IAsyncLifetime
{
    private const long Board = 0x40000001;

    private readonly StubBulletinBoardService _boards = new();
    private readonly ItemService _items = TestItems.Create();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private ItemEntity _board = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        _board = new ItemEntity { Id = new Serial((uint)Board), TemplateId = "bulletin_board", ItemId = 0x1E5E, Amount = 1 };
        _board.PlaceOnGround(MapType.Trammel, new Point3D(1492, 1607, 20));
        _items.Add([_board]);
    }

    [Fact]
    public void Open_OpensTheBoardOnThePlayersClient()
    {
        Assert.True(Run($"return board.open({Board}, 2)")[0].Read<bool>());

        Assert.Equal((_board, _session), Assert.Single(_boards.Opened));
    }

    [Theory,
     // No such item, no such player, an NPC of the world has no client, and numbers that are no serial.
     InlineData("0x4000EEEE, 2"),
     InlineData("0x40000001, 99"),
     InlineData("-1, 2"),
     InlineData("0x40000001, 0"),
     InlineData("99999999999, 2")]
    public void Open_WithoutTheBoardOrThePlayer_IsFalse(string arguments)
    {
        Assert.False(Run($"return board.open({arguments})")[0].Read<bool>());
        Assert.Empty(_boards.Opened);
    }

    [Fact]
    public void Open_AnItemThatIsNotABoard_IsFalse()
    {
        _boards.Boards = _ => false;

        Assert.False(Run($"return board.open({Board}, 2)")[0].Read<bool>());
        Assert.Empty(_boards.Opened);
    }

    [Fact]
    public void Open_GivesWhatTheBoardAnswers()
    {
        _boards.OpenResult = false;

        Assert.False(Run($"return board.open({Board}, 2)")[0].Read<bool>());
    }

    [Fact]
    public void Post_PostsInTheNameGiven_AndGivesTheSerialOfTheMessage()
    {
        var result = Run(
            $"return board.post({Board}, 'The town crier', 'Hear ye', {{ 'The bank is closed.', '', 'Come back tomorrow.' }})"
        );

        var posted = Assert.Single(_boards.PostedAs);
        Assert.Equal(
            (_board, "The town crier", "Hear ye", Serial.Zero),
            (posted.Board, posted.Name, posted.Subject, posted.ReplyTo)
        );
        Assert.Equal(["The bank is closed.", "", "Come back tomorrow."], posted.Lines);
        Assert.Equal(_boards.MessageList[0].Id.Value, (uint)result[0].Read<long>());
    }

    [Fact]
    public void Post_WithAThread_IsAReply()
    {
        Run($"board.post({Board}, 'The stable master', 'Re: Horse', {{ 'Sold.' }}, 0x40001000)");

        Assert.Equal(new Serial(0x40001000), Assert.Single(_boards.PostedAs).ReplyTo);
    }

    // What is not a string in the lines is no line: a number is written as text, a table is left out.
    [Fact]
    public void Post_TakesTheLinesThatAreText_AndNumbersAsText()
    {
        Run($"board.post({Board}, 'The town crier', 'Hear ye', {{ 'one', 2, {{}}, 'three' }})");

        Assert.Equal(["one", "2", "three"], Assert.Single(_boards.PostedAs).Lines);
    }

    [Theory,
     InlineData("0x4000EEEE, 'Crier', 'Hear ye', { 'x' }"),
     InlineData("-1, 'Crier', 'Hear ye', { 'x' }"),
     InlineData("0x40000001, 'Crier', 'Hear ye', { 'x' }, -5"),
     InlineData("0x40000001, 'Crier', 'Hear ye', { 'x' }, 99999999999")]
    public void Post_WithoutTheBoard_OrWithAThreadThatIsNoSerial_IsNil(string arguments)
    {
        Assert.Equal(LuaValue.Nil, Run($"return board.post({arguments})")[0]);
        Assert.Empty(_boards.PostedAs);
    }

    [Fact]
    public void Post_OnWhatIsNotABoard_IsNil()
    {
        _boards.Boards = _ => false;

        Assert.Equal(LuaValue.Nil, Run($"return board.post({Board}, 'Crier', 'Hear ye', {{ 'x' }})")[0]);
        Assert.Empty(_boards.PostedAs);
    }

    [Theory, InlineData(BulletinPostResultType.Empty), InlineData(BulletinPostResultType.Busy)]
    public void Post_ThatTheBoardRefuses_IsNil(BulletinPostResultType refusal)
    {
        _boards.PostAsResult = refusal;

        Assert.Equal(LuaValue.Nil, Run($"return board.post({Board}, 'Crier', '', {{ 'x' }})")[0]);
    }

    [Fact]
    public void Messages_GivesEachMessageOfTheBoard_InItsOrder()
    {
        _boards.MessageList.Add(
            new()
            {
                Id = new Serial(0x40001000), BoardId = _board.Id, PosterId = new Serial(2), PosterName = "Aria",
                Subject = "Horse",
                Body = "A fine mare.\n\nAsk Aria.", PostedAt = 1_791_201_600_000
            }
        );
        _boards.MessageList.Add(
            new()
            {
                Id = new Serial(0x40001001), BoardId = _board.Id, ThreadId = new Serial(0x40001000),
                PosterName = "The stable master",
                Subject = "Re: Horse", Body = "Sold.", PostedAt = 1_791_201_660_000
            }
        );
        _boards.MessageList.Add(
            new() { Id = new Serial(0x40001002), BoardId = new Serial(0x40000002), Subject = "Elsewhere", Body = "x" }
        );

        var result = Run(
            $"""
             local list = board.messages({Board})
             local first, reply = list[1], list[2]
             return #list, first.serial, first.thread, first.poster, first.name, first.subject, #first.lines, first.lines[3], first.posted_at,
                 reply.serial, reply.thread, reply.poster, reply.name
             """
        );

        Assert.Equal(2, result[0].Read<int>());
        Assert.Equal(0x40001000, result[1].Read<long>());
        // A first message has no thread; a message a script posted has no poster.
        Assert.Equal(LuaValue.Nil, result[2]);
        Assert.Equal(2, result[3].Read<long>());
        Assert.Equal(
            ("Aria", "Horse", 3, "Ask Aria."),
            (result[4].Read<string>(), result[5].Read<string>(), result[6].Read<int>(), result[7].Read<string>())
        );
        // In seconds, as os.time gives them.
        Assert.Equal(1_791_201_600, result[8].Read<long>());
        Assert.Equal((0x40001001, 0x40001000), (result[9].Read<long>(), result[10].Read<long>()));
        Assert.Equal(LuaValue.Nil, result[11]);
        Assert.Equal("The stable master", result[12].Read<string>());
    }

    [Theory, InlineData("0x4000EEEE"), InlineData("-1"), InlineData("0")]
    public void Messages_OfWhatIsNotThere_IsAnEmptyTable(string board)
    {
        Assert.Equal(0, Run($"return #board.messages({board})")[0].Read<int>());
    }

    [Fact]
    public void Remove_TakesTheMessageAway_AndIsFalseForOneThatIsNotThere()
    {
        _boards.MessageList.Add(new() { Id = new Serial(0x40001000), BoardId = _board.Id, Subject = "Horse", Body = "x" });

        var result = Run(
            "return board.remove(0x40001000), board.remove(0x40001000), board.remove(-1), board.remove(99999999999)"
        );

        Assert.Equal([true, false, false, false], result.Select(value => value.Read<bool>()));
        Assert.Empty(_boards.MessageList);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new BoardModule(_boards, _items, _fixture.Sessions));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
