using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Pets;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>pet</c> Lua module: what the skill of taming and the scripts of the pets ask about the creatures of a player.
/// </summary>
[ScriptModule("pet", "Asks about the creatures a player has tamed and tames a wild one, as the Animal Taming skill does.")]
public sealed class PetModule
{
    private readonly IPetService _pets;
    private readonly ITamingService _taming;
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly ISessionService _sessions;

    public PetModule(
        IPetService pets,
        ITamingService taming,
        IMobileService mobiles,
        IMobileStateService state,
        ISessionService sessions
    )
    {
        _pets = pets;
        _taming = taming;
        _mobiles = mobiles;
        _state = state;
        _sessions = sessions;
    }

    /// <summary>
    ///     Gets what is known of a creature for taming; <c>pet.info(creature)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "What taming knows of the creature, as { min_skill, slots, owner }: the Animal Taming it takes in points (a try has a chance from 0.1 under it to 49.9 above), the followers it counts for, and the serial of its owner, 0 when it has none. nil for a creature that cannot be tamed, a player, or a serial that is not a mobile in the world."
    )]
    public LuaTable? Info(long creature)
    {
        if (!TryGet(creature, out var mobile) ||
            !mobile.IsNpc ||
            mobile.TemplateId is not { } template ||
            !_taming.TryGet(template, out var entry))
        {
            return null;
        }

        var table = new LuaTable();
        table["min_skill"] = entry.MinSkill;
        table["slots"] = entry.Slots;
        table["owner"] = mobile.GetProp(MountProps.Owner, 0L);

        return table;
    }

    /// <summary>
    ///     Gets how many followers the player has; <c>pet.followers(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "How many followers the player has: the slots of the creatures in the world that are its own, and of the one it rides. 0 for an NPC or a serial that is not in the world."
    )]
    public int Followers(long player)
    {
        return TryGet(player, out var mobile) && !mobile.IsNpc ? _pets.Followers(mobile) : 0;
    }

    /// <summary>
    ///     Gets how many followers a player may have; <c>pet.max_followers()</c>.
    /// </summary>
    [ScriptFunction(helpText: "How many followers a player may have (ultima.pets.max_followers).")]
    public int MaxFollowers()
    {
        return _pets.MaxFollowers;
    }

    /// <summary>
    ///     Makes a creature the player's own; <c>pet.tame(who, creature)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Makes the creature the player's own, when it is a wild creature of the world that can be tamed and its slots fit in the player's followers, and shows the player its new followers. Gives a PetResultType: Ok, NotAnNpc, NotTamable, AlreadyOwned, TooManyFollowers or NoPlayer. It does not roll the skill: the script does that first."
    )]
    public PetResultType Tame(long player, long creature)
    {
        if (!TryGet(player, out var owner))
        {
            return PetResultType.NoPlayer;
        }

        if (!TryGet(creature, out var wild))
        {
            return PetResultType.NotAnNpc;
        }

        var result = _pets.TryTame(owner, wild);

        if (result == PetResultType.Ok && _sessions.TryGetByCharacterId(owner.Id, out var session))
        {
            _state.SendStatus(session, owner);
        }

        return result;
    }

    private bool TryGet(long serial, out MobileEntity mobile)
    {
        mobile = null!;

        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile!);
    }
}
