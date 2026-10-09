using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>vendor</c> Lua module: what a vendor's script does with the shop window of the players.
/// </summary>
[ScriptModule("vendor", "Opens the shop window of a vendor, as a vendor does.")]
public sealed class VendorModule
{
    // Long enough for every vendor that heard the same words in one turn of the loop, short for a player's next ones.
    private static readonly TimeSpan AttendedFor = TimeSpan.FromMilliseconds(500);

    private readonly IVendorService _vendors;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly TimeProvider _time;

    // When each player was last attended to: several vendors hear the same words, one serves.
    private readonly Dictionary<Serial, DateTimeOffset> _attended = new();

    public VendorModule(
        IVendorService vendors,
        IMobileService mobiles,
        ISessionService sessions,
        TimeProvider? time = null
    )
    {
        _vendors = vendors;
        _mobiles = mobiles;
        _sessions = sessions;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    ///     Whether <paramref name="vendor" /> has a shop that sells; <c>vendor.sells(npc)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the NPC has a shop with something to sell to a player. False for an NPC with no shop, for a player and for a serial that is no mobile."
    )]
    public bool Sells(long vendor)
    {
        return TryGetVendor(vendor, out var seller) && _vendors.Sells(seller);
    }

    /// <summary>
    ///     Whether <paramref name="vendor" /> has a shop that buys; <c>vendor.buys(npc)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the NPC has a shop that buys something from a player. False for an NPC with no shop, for a player and for a serial that is no mobile."
    )]
    public bool Buys(long vendor)
    {
        return TryGetVendor(vendor, out var buyer) && _vendors.Buys(buyer);
    }

    /// <summary>
    ///     Opens the buy window of <paramref name="vendor" /> for <paramref name="player" />;
    ///     <c>vendor.open_buy(npc, speaker)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens the vendor's shop window for the player. False when the vendor has no shop or nothing in stock, is more than 10 tiles away or out of sight, when the player is dead or not in the world, or a murderer in a guarded place."
    )]
    public bool OpenBuy(long vendor, long player)
    {
        return TryGetPlayer(player, out var buyer) &&
               TryGetVendor(vendor, out var seller) &&
               _sessions.TryGetByCharacterId(buyer.Id, out var session) &&
               _vendors.OpenBuy(session, seller);
    }

    /// <summary>
    ///     Offers the player's items that <paramref name="vendor" /> buys; <c>vendor.open_sell(npc, player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Offers the player the list of what the player carries that the vendor buys. False when the vendor has no shop that buys, is more than 10 tiles away or out of sight, the player has nothing to sell (the vendor says so), is dead or not in the world, or a murderer in a guarded place."
    )]
    public bool OpenSell(long vendor, long player)
    {
        return TryGetPlayer(player, out var seller) &&
               TryGetVendor(vendor, out var buyer) &&
               _sessions.TryGetByCharacterId(seller.Id, out var session) &&
               _vendors.OpenSell(session, buyer);
    }

    /// <summary>
    ///     Opens the buy window for words the player said, unless another vendor opened one for them a moment ago;
    ///     <c>vendor.open_buy_once(npc, speaker)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens the vendor's shop window for words the player said, unless a vendor opened one for the player within half a second: several vendors side by side hear the same words in the same moment, and a vendor that cannot serve does not stop the others. False when nothing was opened."
    )]
    public bool OpenBuyOnce(long vendor, long player)
    {
        return Once(vendor, player, OpenBuy);
    }

    /// <summary>
    ///     Offers the sell list for words the player said, unless another vendor opened a window for them a moment ago;
    ///     <c>vendor.open_sell_once(npc, speaker)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Offers the player the sell list for words the player said, unless a vendor opened a window for the player within half a second. False when nothing was opened."
    )]
    public bool OpenSellOnce(long vendor, long player)
    {
        return Once(vendor, player, OpenSell);
    }

    private bool Once(long vendor, long player, Func<long, long, bool> open)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return false;
        }

        var now = _time.GetUtcNow();

        if (_attended.TryGetValue(mobile.Id, out var last) && now - last < AttendedFor && now >= last)
        {
            return false;
        }

        if (!open(vendor, player))
        {
            return false;
        }

        // The players who left are forgotten as the list is used.
        if (_attended.Count > 256)
        {
            foreach (var gone in _attended.Where(entry => now - entry.Value >= AttendedFor)
                         .Select(entry => entry.Key)
                         .ToArray())
            {
                _attended.Remove(gone);
            }
        }

        _attended[mobile.Id] = now;

        return true;
    }

    private bool TryGetPlayer(long serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile) &&
               !mobile.IsNpc;
    }

    private bool TryGetVendor(long serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile) &&
               mobile.IsNpc;
    }
}
