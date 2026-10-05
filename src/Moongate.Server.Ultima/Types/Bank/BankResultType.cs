namespace Moongate.Server.Ultima.Types.Bank;

/// <summary>
///     What came of moving gold in or out of a bank.
/// </summary>
public enum BankResultType
{
    /// <summary>The gold moved.</summary>
    Ok = 0,

    /// <summary>There is less gold than was asked for, in the bank for a withdrawal, in the backpack for a deposit.</summary>
    NotEnoughGold = 1,

    /// <summary>More than a banker hands out at one time.</summary>
    TooMuch = 2,

    /// <summary>The backpack has no room for the gold, or cannot hold its weight.</summary>
    BackpackFull = 3,

    /// <summary>The bank box has no room for the gold.</summary>
    BankFull = 4,

    /// <summary>The amount is not a whole number above zero.</summary>
    BadAmount = 5,

    /// <summary>Not a player in the world.</summary>
    NoPlayer = 6,

    /// <summary>The player has no bank box yet: it is made the first time the bank is opened.</summary>
    NoBank = 7,

    /// <summary>No serial was ready for a new pile: the same request works a moment later.</summary>
    Busy = 8
}
