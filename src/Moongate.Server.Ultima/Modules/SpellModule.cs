using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>spell</c> Lua module: the spells of Magery, the spellbook and the cast; <c>spell.cast(user, "heal")</c>.
///     A spell is named by its number, 1 to 64 as the client counts them, or by its key, such as
///     <c>magic_arrow</c>, the key of <c>data/spells.toml</c> and the name of its script.
/// </summary>
[ScriptModule("spell", "The spells of Magery: what a spell is, the spellbook, and casting.")]
public sealed class SpellModule
{
    private readonly ISpellCatalogService _catalog;
    private readonly ISpellbookService _books;
    // Lazy: the cast service calls the spell scripts, whose engine builds this module.
    private readonly Lazy<ISpellCastService> _casts;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly ISessionService _sessions;
    // Lazy: the use handler runs the item scripts, whose engine builds this module.
    private readonly Lazy<IUseService>? _uses;

    public SpellModule(
        ISpellCatalogService catalog,
        ISpellbookService books,
        Lazy<ISpellCastService> casts,
        IMobileService mobiles,
        IItemService items,
        ISessionService sessions,
        Lazy<IUseService>? uses = null
    )
    {
        _uses = uses;
        _catalog = catalog;
        _books = books;
        _casts = casts;
        _mobiles = mobiles;
        _items = items;
        _sessions = sessions;
    }

    /// <summary>
    ///     Casts a spell the mobile has in a spellbook it carries; <c>spell.cast(user, "magic_arrow")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Begins the cast of a spell, by number or key, as the player's own request does: it must have the spell in a spellbook it wears or carries in its backpack, and the usual checks tell it why not (dead, casting, frozen, not recovered, no mana). True when the cast began; then the words, the delay, the target cursor, the reagents, the mana and the skill check follow."
    )]
    public bool Cast(long caster, object spellRef)
    {
        return TryMobile(caster, out var who) && TrySpell(spellRef, out var spell) && _casts.Value.CastFromBook(who, spell.Id);
    }

    /// <summary>
    ///     Casts the spell of a scroll the mobile carries; <c>spell.cast_scroll(user, scroll)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Begins the cast of the spell of a scroll the mobile carries in its backpack, as a double click does: no reagents, a skill window two circles easier, and one scroll used up when it succeeds. True when the cast began; false for a mobile or an item that is none, or a scroll that is no spell."
    )]
    public bool CastScroll(long caster, long scroll)
    {
        return TryMobile(caster, out var who) && TryItem(scroll, out var item) && _casts.Value.CastFromScroll(who, item);
    }

    /// <summary>
    ///     Opens a spellbook the player carries; <c>spell.open_book(user, book)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens a spellbook for a player, showing the spells it holds, as a double click does. The player must wear it or carry it in its backpack (and not in a bag inside it): else it is told so. False for a player, an item or a spellbook that is none, or a book not carried."
    )]
    public bool OpenBook(long player, long book)
    {
        return TryMobile(player, out var who) &&
               TryItem(book, out var item) &&
               _books.IsSpellbook(item) &&
               _sessions.TryGetByCharacterId(who.Id, out var session) &&
               _books.TryOpen(session, who, item);
    }

    /// <summary>
    ///     Whether a spellbook holds a spell; <c>spell.has(book, "heal")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the spellbook holds the spell, by number or key. False for an item that is no spellbook.")]
    public bool Has(long book, object spellRef)
    {
        return TryItem(book, out var item) &&
               _books.IsSpellbook(item) &&
               TrySpell(spellRef, out var spell) &&
               _books.Has(item, spell.Id);
    }

    /// <summary>
    ///     Writes a spell in a spellbook; <c>spell.add(book, "heal")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Writes the spell, by number or key, in the spellbook, as a scroll dropped on it does, and shows the book again. False, with nothing changed, for an item that is no spellbook, an unknown spell or one the book holds already."
    )]
    public bool Add(long book, object spellRef)
    {
        return TryItem(book, out var item) &&
               _books.IsSpellbook(item) &&
               TrySpell(spellRef, out var spell) &&
               _books.Add(item, spell.Id);
    }

    /// <summary>
    ///     The numbers of the spells a spellbook holds; <c>spell.spells(book)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The numbers of the spells the spellbook holds, in order, as a list; nil for an item that is no spellbook.")]
    public LuaTable? Spells(long book)
    {
        if (!TryItem(book, out var item) || !_books.IsSpellbook(item))
        {
            return null;
        }

        var mask = _books.GetSpells(item);
        var table = new LuaTable();
        var count = 0;

        for (var id = 1; id <= 64; id++)
        {
            if ((mask & (1UL << (id - 1))) != 0)
            {
                table[++count] = id;
            }
        }

        return table;
    }

    /// <summary>
    ///     What a spell is; <c>spell.info("magic_arrow").circle</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "What data/spells.toml says of a spell, by number or key, as a table: id, key, name, circle, mantra, mana, target ('none', 'mobile', 'item' or 'location'), harmful, resistable, reflectable, scroll (the template), enabled. Nil for an unknown spell."
    )]
    public LuaTable? Info(object spellRef)
    {
        if (!TrySpell(spellRef, out var spell))
        {
            return null;
        }

        var table = new LuaTable();
        table["id"] = spell.Id;
        table["key"] = spell.Key;
        table["name"] = spell.Name;
        table["circle"] = spell.Circle;
        table["mantra"] = spell.Mantra;
        table["mana"] = SpellCircleRules.Mana(spell.Circle);
        table["target"] = spell.Target.ToString().ToLowerInvariant();
        table["harmful"] = spell.Harmful;
        table["resistable"] = spell.Resistable;
        table["reflectable"] = spell.Reflectable;
        table["scroll"] = spell.Scroll;
        table["enabled"] = spell.Enabled;

        return table;
    }

    /// <summary>
    ///     The spell a scroll holds; <c>spell.of_scroll(scroll)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The number of the spell a scroll item holds; nil for an item that is no scroll of a spell.")]
    public int? OfScroll(long scroll)
    {
        return TryItem(scroll, out var item) && _catalog.TryGetByScrollGraphic(item.ItemId, out var spell) ? spell.Id : null;
    }

    /// <summary>
    ///     Whether a mobile is casting; <c>spell.is_casting(user)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the mobile is casting a spell, its delay running or its target cursor waiting.")]
    public bool IsCasting(long caster)
    {
        return TryMobile(caster, out var who) && _casts.Value.IsCasting(who);
    }

    /// <summary>
    ///     Disturbs the cast of a mobile as damage does; <c>spell.disturb(target)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Disturbs the cast of the mobile as damage does: a player's spell above the first circle, while its delay runs, is ruined (the player is told so and waits to cast again); nothing for a first circle spell, an NPC, a cast waiting for its target or a mobile that is not casting. A curse uses it on its target."
    )]
    public bool Disturb(long caster)
    {
        if (!TryMobile(caster, out var who))
        {
            return false;
        }

        _casts.Value.Hurt(who);

        return true;
    }

    /// <summary>
    ///     Ends the cast of a mobile; <c>spell.cancel(user)</c>.
    /// </summary>
    [ScriptFunction(
        helpText: "Ends the cast of the mobile, if it has one, with no message, and takes its target cursor away. The recovery that the end of the delay already set stays, so a cancel at the cursor still leaves the short wait before the next cast."
    )]
    public bool Cancel(long caster)
    {
        if (!TryMobile(caster, out var who) || !_casts.Value.IsCasting(who))
        {
            return false;
        }

        _casts.Value.Cancel(who);

        return true;
    }

    /// <summary>
    ///     Whether an item can be used from afar, as Telekinesis asks; <c>spell.can_use_from_afar(user, item)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the player could use the item from afar, as the Telekinesis spell does: it has an on_use in its script, or is a container the player carries or that lies on the ground. False for a player or an item that is none, an NPC, or an item another mobile carries."
    )]
    public bool CanUseFromAfar(long player, long item)
    {
        return _uses is not null && TryMobile(player, out var who) && TryItem(item, out var found) &&
               _uses.Value.CanUseFromAfar(who, found);
    }

    /// <summary>
    ///     Uses an item for a player from any distance; <c>spell.use_from_afar(user, door)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Uses the item for the player as a double click would, but from any distance and with no reach check: the on_use of its script runs, or a container is shown open. True when something was done; false for a player or an item that is none, an NPC (it has no client to show a container to), or an item that cannot be used from afar."
    )]
    public bool UseFromAfar(long player, long item)
    {
        return _uses is not null &&
               TryMobile(player, out var who) &&
               _sessions.TryGetByCharacterId(who.Id, out var session) &&
               TryItem(item, out var found) &&
               _uses.Value.UseFromAfar(session, found.Id);
    }

    private bool TrySpell(object spellRef, out SpellDefinition spell)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        spell = null!;

        return spellRef switch
        {
            string key => _catalog.TryGetByKey(key, out spell!),
            long number => _catalog.TryGet((int)Math.Clamp(number, int.MinValue, int.MaxValue), out spell!),
            double number => _catalog.TryGet((int)Math.Clamp(number, int.MinValue, int.MaxValue), out spell!),
            int number => _catalog.TryGet(number, out spell!),
            _ => false
        };
    }

    private bool TryMobile(long serial, out MobileEntity mobile)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        mobile = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile!) &&
               _mobiles.IsInWorld(mobile.Id);
    }

    private bool TryItem(long serial, out ItemEntity item)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        item = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return serial is > 0 and <= uint.MaxValue && _items.TryGet(new Serial((uint)serial), out item!);
    }
}
