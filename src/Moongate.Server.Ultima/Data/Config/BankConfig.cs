namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the bank: how many items a bank box holds, how much gold a banker hands out at one time and
///     the worth of the checks it writes.
/// </summary>
public sealed class BankConfig
{
    public const int MaximumItems = 10_000;

    /// <summary>
    ///     A pile of coins: a withdrawal is one pile.
    /// </summary>
    public const int MaximumWithdraw = 60_000;

    public const int MaximumCheck = 2_000_000_000;

    /// <summary>
    ///     Gets or sets the items a bank box holds, counted with what is inside its bags; 0 for no limit.
    /// </summary>
    public int MaxItems { get; set; } = 125;

    /// <summary>
    ///     Gets or sets the coins a banker hands out at one time.
    /// </summary>
    public int MaxWithdraw { get; set; } = 60_000;

    /// <summary>
    ///     Gets or sets the worth of the smallest check a banker writes.
    /// </summary>
    public int MinCheck { get; set; } = 5000;

    /// <summary>
    ///     Gets or sets the worth of the largest check a banker writes.
    /// </summary>
    public int MaxCheck { get; set; } = 1_000_000;

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        Check("max_items", MaxItems, 0, MaximumItems);
        Check("max_withdraw", MaxWithdraw, 1, MaximumWithdraw);
        Check("max_check", MaxCheck, 1, MaximumCheck);
        Check("min_check", MinCheck, 1, MaxCheck);
    }

    private static void Check(string key, int value, int from, int to)
    {
        if (value < from || value > to)
        {
            throw new InvalidOperationException($"ultima.bank.{key} must be from {from} to {to}, found {value}.");
        }
    }
}
