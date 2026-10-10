using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The spellbooks of Magery: see <see cref="ISpellbookService" />. The spells are the bits of a long prop, the
///     classic client numbers them from 1, and the book is shown to the client as fake items whose amount is the number.
/// </summary>
public sealed class SpellbookService : ISpellbookService
{
    // The serial of the fake item of spell number n, as the classic servers give it: counting down from the top.
    private const uint FakeSerialBase = 0x7FFFFFFF;
    private const int SpellCount = 64;

    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly ISpellCatalogService _catalog;
    private readonly IPacketSendService _sender;
    private readonly ISpeechService _speech;
    private readonly IItemHandlingService _handling;
    private readonly ISessionService _sessions;

    public SpellbookService(
        IItemService items,
        IItemTemplateService templates,
        ISpellCatalogService catalog,
        IPacketSendService sender,
        ISpeechService speech,
        IItemHandlingService handling,
        ISessionService sessions
    )
    {
        _items = items;
        _templates = templates;
        _catalog = catalog;
        _sender = sender;
        _speech = speech;
        _handling = handling;
        _sessions = sessions;
    }

    public bool IsSpellbook(ItemEntity item)
    {
        return item.ItemId == ISpellbookService.BookGraphic;
    }

    public ulong GetSpells(ItemEntity book)
    {
        if (book.TryGetProp<long>(ISpellbookService.SpellsProp, out var mask))
        {
            return unchecked((ulong)mask);
        }

        return _templates.TryGet(book.TemplateId, out var template) &&
               template.Tags?.GetValueOrDefault(ISpellbookService.SpellsTag) is { } tag &&
               ulong.TryParse(tag, out var fromTemplate)
            ? fromTemplate
            : 0;
    }

    public bool Has(ItemEntity book, int spellId)
    {
        return spellId is >= 1 and <= SpellCount && (GetSpells(book) & Bit(spellId)) != 0;
    }

    public bool Add(ItemEntity book, int spellId)
    {
        if (!IsSpellbook(book) || spellId is < 1 or > SpellCount || Has(book, spellId))
        {
            return false;
        }

        book.SetProp(ISpellbookService.SpellsProp, unchecked((long)(GetSpells(book) | Bit(spellId))));

        // The owner sees the book again, with the new spell: a gump that is open gets the list it did not have. Refresh
        // would draw a worn book as one in a container, so only a book nobody carries goes through it.
        if (_items.GetOwner(book) is { } owner &&
            _sessions.GetAll().FirstOrDefault(session => session.CharacterId == owner) is { } owned)
        {
            Open(owned, book);
        }
        else
        {
            _handling.Refresh(book);
        }

        return true;
    }

    public ItemEntity? FindCarried(MobileEntity mobile, int spellId)
    {
        ItemEntity? Pick(IEnumerable<ItemEntity> candidates)
        {
            return candidates.FirstOrDefault(item => IsSpellbook(item) && (spellId == 0 || Has(item, spellId)));
        }

        var worn = Pick(_items.GetWorn(mobile.Id).Where(item => item.Layer is not LayerType.Backpack));

        if (worn is not null)
        {
            return worn;
        }

        return _items.GetWornAt(mobile.Id, LayerType.Backpack) is { } backpack
            ? Pick(_items.GetContents(backpack.Id))
            : null;
    }

    public void Open(GameSession session, ItemEntity book)
    {
        var mask = GetSpells(book);
        var entries = new List<ContainerItemEntry>();

        for (var index = 0; index < SpellCount; index++)
        {
            if ((mask & (1UL << index)) != 0)
            {
                entries.Add(
                    new(new(FakeSerialBase - (uint)index), 0, index + 1, 0, 0, 0, book.Id, default)
                );
            }
        }

        // The client may never have been told about the book: the contents of a pack reach it when the pack is opened.
        if (book.MobileId is not null && book.Layer is not null)
        {
            _sender.TrySend(session.SessionId, new WornItemPacket(book));
        }
        else if (book.ContainerId is not null)
        {
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(book, session.UsesContainerGrid()));
        }

        _sender.TrySend(
            session.SessionId,
            new DisplayContainerPacket(book.Id, ISpellbookService.BookGump, session.UsesHighSeasContainers())
        );
        _sender.TrySend(session.SessionId, ContainerContentPacket.Of(entries, session.UsesContainerGrid()));
    }

    public bool TryOpen(GameSession session, MobileEntity player, ItemEntity book)
    {
        if (!IsCarriedBy(player, book))
        {
            _speech.TellCliloc(player, ISpellbookService.MustBeCarriedMessage);

            return false;
        }

        Open(session, book);

        return true;
    }

    public SpellbookDropType AddScroll(MobileEntity player, ItemEntity book, ItemEntity scroll)
    {
        if (!IsSpellbook(book) ||
            !IsCarriedBy(player, book) ||
            !_catalog.TryGetByScrollGraphic(scroll.ItemId, out var spell))
        {
            return SpellbookDropType.Ignored;
        }

        if (Has(book, spell.Id))
        {
            _speech.TellCliloc(player, ISpellbookService.AlreadyPresentMessage);

            return SpellbookDropType.AlreadyPresent;
        }

        Add(book, spell.Id);
        _handling.Consume(scroll);
        _speech.PlaySound(player.Map, player.Location, ISpellbookService.ScrollAddedSound);

        return SpellbookDropType.Added;
    }

    // The player wears the book, or carries it in the first level of its backpack.
    public bool IsCarriedBy(MobileEntity mobile, ItemEntity book)
    {
        if (book.MobileId == mobile.Id)
        {
            return book.Layer is not LayerType.Backpack;
        }

        return book.ContainerId is { } container &&
               _items.GetWornAt(mobile.Id, LayerType.Backpack)?.Id == container;
    }

    private static ulong Bit(int spellId)
    {
        return 1UL << (spellId - 1);
    }
}
