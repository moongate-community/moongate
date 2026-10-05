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

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
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

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new BoardModule(_boards, _items, _fixture.Sessions));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
