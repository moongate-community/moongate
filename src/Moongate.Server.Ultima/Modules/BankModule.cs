using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>bank</c> Lua module: what a banker's script does with the players' bank boxes.
/// </summary>
[ScriptModule("bank", "Opens the players' bank boxes, as a banker does.")]
public sealed class BankModule
{
    private readonly IBankService _bank;
    private readonly IMobileService _mobiles;

    public BankModule(IBankService bank, IMobileService mobiles)
    {
        _bank = bank;
        _mobiles = mobiles;
    }

    /// <summary>
    ///     Opens the bank box of <paramref name="player" />, making it the first time; it stays open while the player
    ///     stands still. False for a mobile that is not a player in the world; <c>bank.open(speaker)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Opens the player's bank box, made the first time; it stays open while the player stands still. False for an NPC or a player not in the world.")]
    public bool Open(long player)
    {
        return TryGetPlayer(player, out var mobile) && _bank.Open(mobile);
    }

    /// <summary>
    ///     Gets whether the bank box of <paramref name="player" /> is open; <c>bank.is_open(speaker)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the player's bank box is open: the player has not moved since it opened; false for an NPC or a player not in the world.")]
    public bool IsOpen(long player)
    {
        return TryGetPlayer(player, out var mobile) && _bank.IsOpen(mobile);
    }

    private bool TryGetPlayer(long serial, out MobileEntity mobile)
    {
        mobile = null!;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile!) &&
               !mobile.IsNpc;
    }
}
