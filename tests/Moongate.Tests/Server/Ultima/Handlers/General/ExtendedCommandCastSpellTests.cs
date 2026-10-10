using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Magic;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

/// <summary>
///     The spell icon of the client (0xBF subcommand 0x1C): a flag for a book's serial, the serial, the spell from one.
/// </summary>
public sealed class ExtendedCommandCastSpellTests : IAsyncLifetime
{
    private readonly RecordingSpellCastService _casts = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private ItemService _items = null!;
    private ItemEntity _book = null!;
    private ExtendedCommandPacketHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _items = TestItems.Create(_fixture.Sectors);
        _book = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _items.Add([_book]);
        _handler = new(
            TestTooltips.Create(_items, _fixture.Mobiles),
            _fixture.Sender,
            _fixture.Mobiles,
            casts: _casts,
            items: _items
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Subcommand0x1C_WithABook_CastsTheSpellFromThatBook()
    {
        // BF len sub=001C, flag 1, the serial of the book, the spell 5.
        Handle("BF000D001C0001400000020005");

        Assert.Equal([(_aria, 5, (ItemEntity?)_book)], _casts.FromBook);
    }

    [Fact]
    public void Subcommand0x1C_WithoutABook_CastsTheSpellFromAnyBook()
    {
        Handle("BF0009001C00000005");

        Assert.Equal([(_aria, 5, (ItemEntity?)null)], _casts.FromBook);
    }

    [Fact]
    public void Subcommand0x1C_ASerialThatIsNoItem_CastsFromAnyBook()
    {
        Handle("BF000D001C0001400000990005");

        Assert.Equal([(_aria, 5, (ItemEntity?)null)], _casts.FromBook);
    }

    [Theory]
    [InlineData("BF0007001C0000")]
    [InlineData("BF0009001C00010000")]
    [InlineData("BF0007001C0001")]
    public void Subcommand0x1C_TooShort_CastsNothing(string hex)
    {
        Handle(hex);

        Assert.Empty(_casts.FromBook);
    }

    [Fact]
    public async Task Subcommand0x1C_OfASessionWithoutACharacterInTheWorld_CastsNothing()
    {
        var lobby = await _fixture.AddAsync(3, false);
        Assert.True(ExtendedCommandPacket.TryParse(Convert.FromHexString("BF0009001C00000005"), out var packet));

        _handler.Handle(lobby, packet);

        Assert.Empty(_casts.FromBook);
    }

    private void Handle(string hex)
    {
        Assert.True(ExtendedCommandPacket.TryParse(Convert.FromHexString(hex), out var packet));

        _handler.Handle(_session, packet);
    }
}
