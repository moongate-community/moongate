using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Services.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Magic;

[TestFixture]
public sealed class SpellbookServiceTests
{
    private FakeCharacterService _characterService = null!;
    private SpellbookService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _characterService = new FakeCharacterService();
        _service = new SpellbookService(_characterService);
    }

    [Test]
    public void GetData_WhenContentIsMissing_ReturnsEmptyBook()
    {
        var book = CreateSpellbook();

        var data = _service.GetData(book);

        Assert.That(data.Content, Is.EqualTo(0UL));
    }

    [Test]
    public void SetData_PersistsSpellbookBitfield()
    {
        var book = CreateSpellbook();
        var data = new SpellbookData(0UL).WithSpell(4);

        _service.SetData(book, data);

        Assert.That(book.TryGetCustomInteger(ItemCustomParamKeys.Spellbook.Content, out var stored), Is.True);
        Assert.That(stored, Is.EqualTo((long)data.Content));
    }

    [Test]
    public async Task MobileHasSpellAsync_WhenEquippedSpellbookContainsSpell_ReturnsTrue()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000001u
        };
        var book = CreateSpellbook();
        _service.SetData(book, new SpellbookData(0UL).WithSpell(4));
        mobile.EquipItem(ItemLayerType.OneHanded, book);

        var result = await _service.MobileHasSpellAsync(mobile, SpellbookType.Regular, 4);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task MobileHasSpellAsync_WhenBackpackSpellbookContainsSpell_ReturnsTrue()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000001u
        };
        var backpack = new UOItemEntity();
        var book = CreateSpellbook();
        _service.SetData(book, new SpellbookData(0UL).WithSpell(4));
        backpack.AddItem(book, Point2D.Zero);
        _characterService.Backpack = backpack;

        var result = await _service.MobileHasSpellAsync(mobile, SpellbookType.Regular, 4);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task MobileHasSpellAsync_WhenSpellbookDoesNotContainSpell_ReturnsFalse()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000001u
        };
        var book = CreateSpellbook();
        _service.SetData(book, new SpellbookData(0UL).WithSpell(5));
        mobile.EquipItem(ItemLayerType.OneHanded, book);

        var result = await _service.MobileHasSpellAsync(mobile, SpellbookType.Regular, 4);

        Assert.That(result, Is.False);
    }

    private static UOItemEntity CreateSpellbook()
    {
        var book = new UOItemEntity();
        book.SetCustomString(ItemCustomParamKeys.Item.TemplateId, "spellbook");

        return book;
    }

    private sealed class FakeCharacterService : ICharacterService
    {
        public UOItemEntity? Backpack { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            throw new NotSupportedException();
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            throw new NotSupportedException();
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            throw new NotSupportedException();
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult(Backpack);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
        {
            throw new NotSupportedException();
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            throw new NotSupportedException();
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            throw new NotSupportedException();
        }
    }
}
