using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.ContextMenus;
using Moongate.Server.Ultima.Data.Internal.ContextMenus;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.ContextMenus;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc />
/// <remarks>
///     As ModernUO, the menu sent is remembered and the choice is checked against it: a client cannot choose what it
///     was not offered. As Sphere, the server's entries come first and a script adds its own.
/// </remarks>
public sealed class ContextMenuService : IContextMenuService
{
    /// <summary>
    ///     The function of a mobile or item script that gives the entries it adds for a player.
    /// </summary>
    public const string EntriesFunction = "on_context_menu";

    /// <summary>
    ///     The function of a mobile or item script that is told which of its entries a player chose.
    /// </summary>
    public const string SelectFunction = "on_context_menu_select";

    /// <summary>
    ///     The client's "Open Paperdoll".
    /// </summary>
    public const int PaperdollCliloc = 3006123;

    /// <summary>
    ///     The client's "Open Backpack".
    /// </summary>
    public const int BackpackCliloc = 3006145;

    /// <summary>
    ///     The most entries of one menu.
    /// </summary>
    public const int MaxEntries = 20;

    /// <summary>
    ///     The farthest an entry can be chosen from, and its range when a script gives none.
    /// </summary>
    public const int MaxRange = 18;

    private readonly ILogger _logger = Log.ForContext<ContextMenuService>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IItemTemplateService _templates;
    private readonly IPacketSendService _sender;
    private readonly IUseService _use;
    private readonly WorldConfig _world;
    private readonly INpcScriptService? _npcScripts;
    private readonly IItemScriptService? _itemScripts;
    private readonly IBankService? _bank;

    public ContextMenuService(
        IItemService items,
        IMobileService mobiles,
        IItemTemplateService templates,
        IPacketSendService sender,
        IUseService use,
        WorldConfig world,
        INpcScriptService? npcScripts = null,
        IItemScriptService? itemScripts = null,
        IBankService? bank = null
    )
    {
        _bank = bank;
        _items = items;
        _mobiles = mobiles;
        _templates = templates;
        _sender = sender;
        _use = use;
        _world = world;
        _npcScripts = npcScripts;
        _itemScripts = itemScripts;
    }

    public bool Request(GameSession session, Serial target)
    {
        // A new request takes the place of the menu kept, also when it offers nothing.
        session.Set(ContextMenuSessionKeys.Sent, null);

        if (!_mobiles.TryGet(session.CharacterId, out var player) || !TryLocate(session, player, target, out var place))
        {
            return false;
        }

        var entries = new List<ContextMenuEntry>();

        if (_mobiles.TryGet(target, out var mobile))
        {
            AddServerEntries(player, mobile, entries);

            // The dead are answered by nobody: a ghost gets the server's entries and none of a script.
            if (mobile.IsNpc && !player.IsDead && _npcScripts is not null)
            {
                AddScriptEntries(_npcScripts.Run(mobile, EntriesFunction, (long)player.Id.Value), target, entries);
            }
        }
        else if (!player.IsDead && _items.TryGet(target, out var item) && _itemScripts is not null && _itemScripts.HasScript(item))
        {
            AddScriptEntries(_itemScripts.Run(item, EntriesFunction, (long)player.Id.Value), target, entries);
        }

        if (entries.Count == 0)
        {
            return false;
        }

        if (entries.Count > MaxEntries)
        {
            _logger.Warning("The context menu of {Target} has {Count} entries: only the first {Max} are shown", target, entries.Count, MaxEntries);
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        var shown = entries.Select(entry => (entry.Cliloc, !entry.Enabled || !InRange(player, place, entry.Range))).ToArray();

        if (!_sender.TrySend(session.SessionId, new DisplayContextMenuPacket(target, shown)))
        {
            return false;
        }

        session.Set(ContextMenuSessionKeys.Sent, new(target, entries));

        return true;
    }

    public bool Select(GameSession session, Serial target, int index)
    {
        // Good for one choice: forgotten first, whatever follows.
        var sent = session.Get(ContextMenuSessionKeys.Sent);
        session.Set(ContextMenuSessionKeys.Sent, null);

        if (sent is not null)
        {
            index = PlaceOf(sent, index);
        }

        if (sent is null ||
            sent.Target != target ||
            index < 0 ||
            index >= sent.Entries.Count ||
            !_mobiles.TryGet(session.CharacterId, out var player) ||
            !TryLocate(session, player, target, out var place))
        {
            return false;
        }

        var entry = sent.Entries[index];

        if (!entry.Enabled || !InRange(player, place, entry.Range))
        {
            return false;
        }

        if (entry.ScriptId is { } id)
        {
            // Dead since the menu was shown.
            if (player.IsDead)
            {
                return false;
            }

            if (_mobiles.TryGet(target, out var npc))
            {
                _npcScripts?.Queue(npc, SelectFunction, (long)player.Id.Value, id);
            }
            else if (_items.TryGet(target, out var item))
            {
                _itemScripts?.Queue(item, SelectFunction, (long)player.Id.Value, id);
            }

            return true;
        }

        // The server's own entries do what a double click does, with its checks.
        if (entry.Cliloc == BackpackCliloc)
        {
            if (BackpackOf(player) is not { } backpack)
            {
                return false;
            }

            _use.Use(session, backpack.Id);

            return true;
        }

        _use.Use(session, target);

        return true;
    }

    // The Enhanced Client names an entry chosen from one of its icons by a fixed number, which stands for a text:
    // the place is that of the entry of the menu sent that shows it; none, when the menu has no such entry.
    private static int PlaceOf(SentContextMenu sent, int index)
    {
        if (index < EnhancedClientMenuIndexes.First)
        {
            return index;
        }

        if (!EnhancedClientMenuIndexes.TryGetCliloc(index, out var cliloc))
        {
            return -1;
        }

        for (var place = 0; place < sent.Entries.Count; place++)
        {
            if (sent.Entries[place].Cliloc == cliloc)
            {
                return place;
            }
        }

        return -1;
    }

    private void AddServerEntries(MobileEntity player, MobileEntity mobile, List<ContextMenuEntry> entries)
    {
        if (_use.HasPaperdoll(mobile))
        {
            entries.Add(new(PaperdollCliloc, MaxRange, true, null));
        }

        if (mobile.Id == player.Id && !player.IsDead && BackpackOf(player) is not null)
        {
            entries.Add(new(BackpackCliloc, MaxRange, true, null));
        }
    }

    // What a script answers is not trusted: an entry that is not well formed is left out.
    private void AddScriptEntries(ScriptResult result, Serial target, List<ContextMenuEntry> entries)
    {
        if (result is not { Kind: ScriptResultKind.Completed, Values: [LuaTable table, ..] })
        {
            return;
        }

        for (var index = 1; index <= table.ArrayLength; index++)
        {
            if (table[index].TryRead<LuaTable>(out var entry) &&
                entry["id"].TryRead<string>(out var id) &&
                !string.IsNullOrWhiteSpace(id) &&
                entry["cliloc"].TryRead<double>(out var cliloc) &&
                cliloc is > 0 and <= int.MaxValue &&
                Math.Floor(cliloc) == cliloc &&
                TryRange(entry["range"], out var range))
            {
                var enabled = !entry["enabled"].TryRead<bool>(out var state) || state;
                entries.Add(new((int)cliloc, range, enabled, id));
            }
            else
            {
                _logger.Warning("The script of {Target} gave a context menu entry that is not one, at position {Position}: left out", target, index);
            }
        }
    }

    private static bool TryRange(LuaValue value, out int range)
    {
        range = MaxRange;

        if (value.Type == LuaValueType.Nil)
        {
            return true;
        }

        if (!value.TryRead<double>(out var tiles) || tiles is < 0 or > MaxRange || Math.Floor(tiles) != tiles)
        {
            return false;
        }

        range = (int)tiles;

        return true;
    }

    // Where the target is for the player: a mobile it sees, or an item it carries or sees on the ground or in a
    // container there, on its map and in view.
    private bool TryLocate(GameSession session, MobileEntity player, Serial target, out Point3D place)
    {
        place = default;
        MapType map;

        if (_mobiles.TryGet(target, out var mobile))
        {
            if (mobile.IsHiddenFrom(player.Id, session.AccountType))
            {
                return false;
            }

            map = mobile.Map;
            place = mobile.Location;
        }
        else if (_items.TryGet(target, out var item))
        {
            var visibility = item.Visibility ?? (_templates.TryGet(item.TemplateId, out var template) ? template.Visibility : null);

            if (session.AccountType < (visibility ?? AccountType.Regular))
            {
                return false;
            }

            if (_items.GetOwner(item) is { } owner)
            {
                // What another mobile carries is not this player's to ask about, nor is what lies in its own bank
                // while the bank is closed: the reach of a double click.
                if (owner != player.Id || _bank?.CanAccess(session, player, item) == false)
                {
                    return false;
                }

                map = player.Map;
                place = player.Location;
            }
            else if (_items.GetGroundRoot(item) is { Map: { } itemMap, GroundLocation: { } ground } root && _items.IsLyingOnGround(root))
            {
                map = itemMap;
                place = ground;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        return map == player.Map && InRange(player, place, _world.ViewRange);
    }

    private static bool InRange(MobileEntity player, Point3D place, int range)
    {
        return Math.Abs(player.Location.X - place.X) <= range && Math.Abs(player.Location.Y - place.Y) <= range;
    }

    private ItemEntity? BackpackOf(MobileEntity mobile)
    {
        return _items.GetWorn(mobile.Id).FirstOrDefault(item => item.Layer == LayerType.Backpack);
    }
}
