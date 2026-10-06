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
    // Long enough for every banker that heard the same words in one turn of the loop, short for a player's next ones.
    private static readonly TimeSpan AttendedFor = TimeSpan.FromMilliseconds(500);

    private readonly IBankService _bank;
    private readonly IMobileService _mobiles;
    private readonly TimeProvider _time;
    private readonly IItemService? _items;

    // When each player was last attended to: several bankers hear the same words, one serves.
    private readonly Dictionary<Serial, DateTimeOffset> _attended = new();

    public BankModule(IBankService bank, IMobileService mobiles, TimeProvider? time = null, IItemService? items = null)
    {
        _bank = bank;
        _mobiles = mobiles;
        _time = time ?? TimeProvider.System;
        _items = items;
    }

    /// <summary>
    ///     Gets whether the caller is the one that serves <paramref name="player" /> now: true for the first that
    ///     asks, false for the others in the same moment; <c>if not bank.attend(speaker) then return end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the caller is the one that serves the player now: true for the first that asks, false for whoever asks again within half a second. Several bankers behind one counter hear the same words in the same moment: each asks, one answers, and the gold moves once. False for an NPC or a player not in the world."
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
    ///     Opens the bank box of <paramref name="player" />, making it the first time; it stays open while the player
    ///     stands still. False for a mobile that is not a player in the world; <c>bank.open(speaker)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Opens the player's bank box, made the first time; it stays open while the player stands still. False for an NPC or a player not in the world."
    )]
    public bool Open(long player)
    {
        return TryGetPlayer(player, out var mobile) && _bank.Open(mobile);
    }

    /// <summary>
    ///     Gets whether the bank box of <paramref name="player" /> is open; <c>bank.is_open(speaker)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the player's bank box is open: the player has not moved since it opened; false for an NPC or a player not in the world."
    )]
    public bool IsOpen(long player)
    {
        return TryGetPlayer(player, out var mobile) && _bank.IsOpen(mobile);
    }

    /// <summary>
    ///     Gets the gold in the bank of <paramref name="player" />; <c>bank.balance(speaker)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The gold in the player's bank: the coins anywhere inside its bank box, bags included, and what the bank checks there are worth; 0 for a player who never opened its bank; nil for an NPC or a player not in the world. The box need not be open."
    )]
    public int? Balance(long player)
    {
        return TryGetPlayer(player, out var mobile) ? _bank.Balance(mobile) : null;
    }

    /// <summary>
    ///     Moves coins from the bank of <paramref name="player" /> to its backpack;
    ///     <c>bank.withdraw(speaker, 500) == BankResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Moves amount coins from the player's bank to its backpack, onto a gold pile already there when it fits; the coins of the bank go first and the bank checks give the rest, one used up is gone and the last keeps what is left. Gives a BankResultType: Ok, BadAmount (not a whole number above 0), TooMuch (more than ultima.bank.max_withdraw), NoBank (the player never opened its bank: bank.open makes it), NotEnoughGold, BackpackFull (no backpack, no room for a new pile, or a backpack already at its weight: one that is not takes the gold whatever it weighs, and the player may walk away overloaded), Busy (try again in a moment) or NoPlayer (an NPC or a player not in the world). All or nothing: a refusal moves no coin. The box need not be open, and the function does not check where the player stands or who it is: a banker script checks the distance and mobile.criminal first."
    )]
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
    [ScriptFunction(
        helpText:
        "Moves amount coins from the player's backpack and its bags to its bank box, topping up the gold piles of the box and then making piles of 60000, and gives a BankResultType: Ok, BadAmount (not a whole number above 0), NoBank (the player never opened its bank: bank.open makes it), NotEnoughGold, BankFull (the box holds ultima.bank.max_items), Busy (try again in a moment) or NoPlayer (an NPC or a player not in the world). All or nothing: a refusal moves no coin. The box need not be open."
    )]
    public BankResultType Deposit(long player, double amount)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return BankResultType.NoPlayer;
        }

        return IsWhole(amount) ? _bank.Deposit(mobile, (int)amount) : BankResultType.BadAmount;
    }

    /// <summary>
    ///     Puts a gold pile or a bank check into the bank of <paramref name="player" />;
    ///     <c>bank.deposit_item(giver, item) == BankResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Puts a gold pile or a bank check into the player's bank box, as a banker does with what is dropped on it (on_drag_drop): the gold tops up the piles of the box and what is left is a pile of its own, a check goes in worth the same. Gives a BankResultType: Ok (the item handed over is gone: return true from on_drag_drop), NotMoney (not gold nor a bank check, not there, worn, on a cursor or carried by someone else), NoBank (the player never opened its bank: bank.open makes it), BankFull (the box holds ultima.bank.max_items), Busy (try again in a moment) or NoPlayer. All or nothing. bank.balance before and after tells how much went in. The box need not be open."
    )]
    public BankResultType DepositItem(long player, long item)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return BankResultType.NoPlayer;
        }

        if (_items is null || item is <= 0 or > uint.MaxValue || !_items.TryGet(new Serial((uint)item), out var found))
        {
            return BankResultType.NotMoney;
        }

        return _bank.DepositItem(mobile, found);
    }

    /// <summary>
    ///     Writes a bank check paid with the coins of the bank of <paramref name="player" />;
    ///     <c>bank.check(speaker, 5000) == BankResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Writes a bank check worth amount, paid with the coins of the player's bank and put in its bank box, and gives a BankResultType: Ok, BadAmount (not a whole number above 0), CheckTooSmall (below ultima.bank.min_check), CheckTooBig (above ultima.bank.max_check), NoBank (the player never opened its bank), NotEnoughGold (coins only: another check does not pay a check), BankFull (the box holds ultima.bank.max_items and no gold pile is used up to leave its place), Busy (try again in a moment) or NoPlayer. All or nothing. The check is an item of the template bank_check; bank.worth reads what it is worth."
    )]
    public BankResultType Check(long player, double amount)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return BankResultType.NoPlayer;
        }

        return IsWhole(amount) ? _bank.WriteCheck(mobile, (int)amount) : BankResultType.BadAmount;
    }

    /// <summary>
    ///     Turns a bank check inside the bank box of <paramref name="player" /> into coins;
    ///     <c>bank.cash(user, check) == BankResultType.Ok</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Turns a bank check lying inside the player's bank box, in the box or in a bag of it, into coins of the box: the gold piles there are topped up and piles of 60000 are made. Gives a BankResultType: Ok when gold went in, all of it and the check is gone, or what the box had room for and the check keeps the rest (bank.worth before and after tells how much went in); BankFull when nothing fits; NotInBank for an item that is not a check or is not inside that player's bank box; NoBank, Busy or NoPlayer. It does not ask whether the box is open: an item script's on_use is not called for an item of a closed bank."
    )]
    public BankResultType Cash(long player, long check)
    {
        if (!TryGetPlayer(player, out var mobile))
        {
            return BankResultType.NoPlayer;
        }

        if (_items is null || check is <= 0 or > uint.MaxValue || !_items.TryGet(new Serial((uint)check), out var item))
        {
            return BankResultType.NotInBank;
        }

        return _bank.Cash(mobile, item, out _);
    }

    /// <summary>
    ///     Gets what a bank check is worth; <c>bank.worth(check)</c>.
    /// </summary>
    [ScriptFunction(
        helpText: "What a bank check is worth in gold; nil for an item that is not a bank check, or is not there."
    )]
    public long? Worth(long item)
    {
        return _items is not null && item is > 0 and <= uint.MaxValue && _items.TryGet(new Serial((uint)item), out var found)
            ? _bank.WorthOf(found)
            : null;
    }

    // Lua numbers: an amount with a fraction, or beyond an int, is none.
    private static bool IsWhole(double value)
    {
        return value is >= int.MinValue and <= int.MaxValue && Math.Floor(value) == value;
    }

    private bool TryGetPlayer(long serial, out MobileEntity mobile)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        mobile = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile!) &&
               !mobile.IsNpc;
    }
}
