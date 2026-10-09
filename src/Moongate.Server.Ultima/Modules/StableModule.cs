using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Stable;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>stable</c> Lua module: what a stablemaster's script does with the pets of the players.
/// </summary>
[ScriptModule("stable", "Leaves a player's pets in the stable and takes them back, as a stablemaster does.")]
public sealed class StableModule
{
    private const int AttendedLimit = 256;

    // Long enough for every stablemaster that heard the same words in one turn of the loop, short for a player's next ones.
    private static readonly TimeSpan AttendedFor = TimeSpan.FromMilliseconds(500);

    private readonly IStableService _stable;
    private readonly IMobileService _mobiles;
    private readonly IMobileTemplateService _templates;
    private readonly StableConfig _config;
    private readonly TimeProvider _time;

    // When each player was last attended to: several stablemasters hear the same words, one serves.
    private readonly Dictionary<Serial, DateTimeOffset> _attended = new();

    public StableModule(
        IStableService stable,
        IMobileService mobiles,
        IMobileTemplateService templates,
        StableConfig config,
        TimeProvider? time = null
    )
    {
        _stable = stable;
        _mobiles = mobiles;
        _templates = templates;
        _config = config;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    ///     Gets whether the caller is the one that serves <paramref name="player" /> now; <c>if not stable.attend(who) then
    ///     return end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the caller is the one that serves the player now: true for the first that asks, false for whoever asks again within half a second. Several stablemasters in one square hear the same words in the same moment: each asks, one answers. False for an NPC or a player not in the world."
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
        if (_attended.Count > AttendedLimit)
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
    ///     Gets the pets the player has in the stable; <c>stable.pets(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The pets the player has in the stable, in the order they were left, as a list of { template, name } (the name is the one of the template, such as a horse); an empty list for none, nil for an NPC or a player not in the world. A pet is claimed by its place in this list, counted from 1."
    )]
    public LuaTable? Pets(long player)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return null;
        }

        var list = new LuaTable();
        var place = 1;

        foreach (var template in _stable.Stabled(mobile))
        {
            var entry = new LuaTable();
            entry["template"] = template;
            entry["name"] = _templates.TryGet(template, out var found) && !string.IsNullOrEmpty(found.Name)
                ? found.Name
                : template;
            list[place++] = entry;
        }

        return list;
    }

    /// <summary>
    ///     Leaves the creature in the stable of the player; <c>stable.stable(who, pet)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Leaves the pet in the stable of the player and takes the fee from its backpack and then its bank. Gives a StableResultType: Ok, NotAPet (no mount, or not in the world), NotYours, TooFar (more than a tile), Dying, Full, NoGold, NoPlayer or Failed. The caller says how far the player may be from the stablemaster."
    )]
    public StableResultType Stable(long player, long pet)
    {
        if (!TryGetPlayer(player, out var owner))
        {
            return StableResultType.NoPlayer;
        }

        return TryGet(pet, out var creature) ? _stable.TryStable(owner, creature) : StableResultType.NotAPet;
    }

    /// <summary>
    ///     Takes a pet out of the stable; <c>stable.claim(who, place, template)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Takes the pet at place (from 1, as in stable.pets) out of the stable and makes it again beside the player, with the player as its owner. The template must be the one of that place, so a list that changed since it was shown is not claimed by mistake. Gives a StableResultType: Ok, NoPlayer, BadIndex (no such pet at that place) or Failed (the template is gone from the data: the place is dropped)."
    )]
    public StableResultType Claim(long player, int place, string template)
    {
        return TryGetPlayer(player, out var owner)
            ? _stable.TryClaim(owner, place - 1, template)
            : StableResultType.NoPlayer;
    }

    /// <summary>
    ///     Gets how many pets a player may leave; <c>stable.max_pets()</c>.
    /// </summary>
    [ScriptFunction(helpText: "How many pets a player may leave in the stable (ultima.stable.max_pets).")]
    public int MaxPets()
    {
        return _config.MaxPets;
    }

    /// <summary>
    ///     Gets the fee of a pet; <c>stable.fee()</c>.
    /// </summary>
    [ScriptFunction(helpText: "The gold a pet costs when it is stabled (ultima.stable.fee); 0 for free.")]
    public int Fee()
    {
        return _config.Fee;
    }

    private bool TryGetPlayer(long serial, out MobileEntity mobile)
    {
        return TryGet(serial, out mobile) && !mobile.IsNpc;
    }

    private bool TryGet(long serial, out MobileEntity mobile)
    {
        mobile = null!;

        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile!);
    }
}
