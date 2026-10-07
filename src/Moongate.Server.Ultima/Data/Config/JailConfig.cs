namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the jail: the fine a prisoner pays when its sentence ends and the longest sentence.
/// </summary>
public sealed class JailConfig
{
    public const int MaximumFine = 1_000_000_000;
    public const int MaximumDays = 3650;

    /// <summary>
    ///     Gets or sets the gold coins taken from a prisoner when its sentence ends, from its backpack and then its
    ///     bank box; 0 takes nothing.
    /// </summary>
    public int FineGold { get; set; } = 500;

    /// <summary>
    ///     Gets or sets the longest sentence in days.
    /// </summary>
    public int MaxDays { get; set; } = 30;

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (FineGold is < 0 or > MaximumFine)
        {
            throw new InvalidOperationException($"ultima.jail.fine_gold must be from 0 to {MaximumFine}, found {FineGold}.");
        }

        if (MaxDays is < 1 or > MaximumDays)
        {
            throw new InvalidOperationException($"ultima.jail.max_days must be from 1 to {MaximumDays}, found {MaxDays}.");
        }
    }
}
