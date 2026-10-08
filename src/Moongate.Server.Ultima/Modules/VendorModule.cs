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
    ///     Gets whether the caller is the one that serves <paramref name="player" /> now: true for the first that
    ///     asks, false for the others in the same moment; <c>if not vendor.attend(speaker) then return end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the caller is the one that serves the player now: true for the first that asks, false for whoever asks again within half a second. Several vendors side by side hear the same words in the same moment: each asks, one answers. False for an NPC or a player not in the world."
    )]
    public bool Attend(long player)
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
