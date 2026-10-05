using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Bank;

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

    /// <summary>
    ///     Gets the gold in the bank of <paramref name="player" />; <c>bank.balance(speaker)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The gold in the player's bank: the coins anywhere inside its bank box, bags included; 0 for a player who never opened its bank; nil for an NPC or a player not in the world. The box need not be open.")]
    public int? Balance(long player)
    {
        return TryGetPlayer(player, out var mobile) ? _bank.Balance(mobile) : null;
    }

    /// <summary>
    ///     Moves coins from the bank of <paramref name="player" /> to its backpack;
    ///     <c>bank.withdraw(speaker, 500) == BankResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(helpText: "Moves amount coins from the player's bank to its backpack, onto a gold pile already there when it fits, and gives a BankResultType: Ok, BadAmount (not a whole number above 0), TooMuch (more than ultima.bank.max_withdraw), NoBank (the player never opened its bank: bank.open makes it), NotEnoughGold, BackpackFull (no backpack, no room or too heavy), Busy (try again in a moment) or NoPlayer (an NPC or a player not in the world). All or nothing: a refusal moves no coin. The box need not be open, and the function does not check where the player stands or who it is: a banker script checks the distance and mobile.criminal first.")]
    public BankResultType Withdraw(long player, double amount)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return BankResultType.NoPlayer;
        }

        return IsWhole(amount) ? _bank.Withdraw(mobile, (int)amount) : BankResultType.BadAmount;
    }

    /// <summary>
    ///     Moves coins from the backpack of <paramref name="player" /> to its bank;
    ///     <c>bank.deposit(speaker, 500) == BankResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(helpText: "Moves amount coins from the player's backpack and its bags to its bank box, topping up the gold piles of the box and then making piles of 60000, and gives a BankResultType: Ok, BadAmount (not a whole number above 0), NoBank (the player never opened its bank: bank.open makes it), NotEnoughGold, BankFull (the box holds ultima.bank.max_items), Busy (try again in a moment) or NoPlayer (an NPC or a player not in the world). All or nothing: a refusal moves no coin. The box need not be open.")]
    public BankResultType Deposit(long player, double amount)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return BankResultType.NoPlayer;
        }

        return IsWhole(amount) ? _bank.Deposit(mobile, (int)amount) : BankResultType.BadAmount;
    }

    // Lua numbers: an amount with a fraction, or beyond an int, is none.
    private static bool IsWhole(double value)
    {
        return value is >= int.MinValue and <= int.MaxValue && Math.Floor(value) == value;
    }

    private bool TryGetPlayer(long serial, out MobileEntity mobile)
    {
        mobile = null!;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile!) &&
               !mobile.IsNpc;
    }
}
