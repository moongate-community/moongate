using System.Text;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Skills;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Magic;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Skills;

/// <summary>
///     The spell requests of the text command (0x12): 0x27 from the spellbook, 0x56 from a macro, 0x43 open the book.
/// </summary>
public sealed class TextCommandPacketHandlerSpellTests : IAsyncLifetime
{
    private readonly RecordingSkillUseService _skills = new();
    private readonly RecordingSpellCastService _casts = new();
    private readonly StubSpellbookService _books = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private ItemService _items = null!;
    private ItemEntity _book = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _items = TestItems.Create(_fixture.Sectors);
        _book = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _items.Add([_book]);
        _books.Carried.Add(_book);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Theory]
    [InlineData(0x27, "5")]
    [InlineData(0x56, "5")]
    public void Handle_ACastRequest_CastsTheSpellByItsNumberFromOne(byte kind, string text)
    {
        Handle(kind, text);

        var cast = Assert.Single(_casts.FromBook);
        Assert.Equal((_aria, 5, (ItemEntity?)null), cast);
    }

    [Fact]
    public void Handle_ACastRequestFromTheBook_NamesTheBookBySerial_DecimalOrHex()
    {
        Handle(0x27, "5 1073741826");
        Handle(0x27, "6 0x40000002");

        Assert.Equal([(_book, 5), (_book, 6)], _casts.FromBook.Select(cast => (cast.Book!, cast.Spell)));
    }

    [Fact]
    public void Handle_ABookSerialThatIsNone_CastsFromAnyBook()
    {
        Handle(0x27, "5 1");
        Handle(0x56, "5 1073741826");

        Assert.All(_casts.FromBook, cast => Assert.Null(cast.Book));
    }

    [Theory]
    [InlineData("")]
    [InlineData("heal")]
    [InlineData("-1")]
    public void Handle_ACastRequestWithNoNumber_CastsNothing(string text)
    {
        Handle(0x27, text);

        Assert.Empty(_casts.FromBook);
    }

    [Fact]
    public void Handle_OpenTheSpellbook_OpensTheBookTheCharacterCarries()
    {
        Handle(0x43, "1");

        Assert.Equal([_book], _books.Opened);
    }

    [Fact]
    public void Handle_OpenAnotherKindOfBook_OpensNothing()
    {
        Handle(0x43, "2");

        Assert.Empty(_books.Opened);
    }

    [Fact]
    public void Handle_OpenTheSpellbookWithNone_DoesNothing()
    {
        _books.Carried.Clear();

        Handle(0x43, "1");

        Assert.Empty(_books.Opened);
    }

    [Fact]
    public async Task Handle_WithNoCharacterInTheWorld_DoesNothing()
    {
        var stranger = await _fixture.AddAsync(9, entered: false);

        Handle(0x27, "5", stranger);
        Handle(0x43, "1", stranger);

        Assert.Empty(_casts.FromBook);
        Assert.Empty(_books.Opened);
    }

    private void Handle(byte kind, string text, GameSession? session = null)
    {
        var body = Encoding.ASCII.GetBytes(text);
        var data = new byte[body.Length + 5];
        data[0] = 0x12;
        data[1] = (byte)(data.Length >> 8);
        data[2] = (byte)data.Length;
        data[3] = kind;
        body.CopyTo(data, 4);
        Assert.True(TextCommandPacket.TryParse(data, out var packet));

        new TextCommandPacketHandler(_skills, _casts, _books, _fixture.Mobiles, _items).Handle(session ?? _session, packet);
    }
}
