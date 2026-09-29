namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for player characters.
/// </summary>
public sealed class CharactersConfig
{
    private static readonly int[] SupportedSlotCounts = [1, 5, 6, 7];

    /// <summary>
    ///     Gets or sets how many characters an account may hold: 1, 5, 6 or 7, the slot counts the client supports.
    /// </summary>
    public int MaxPerAccount { get; set; } = 7;

    /// <summary>
    ///     Gets or sets how many hours a character stays restorable after the player asks to delete it; at least 1.
    /// </summary>
    public int DeletionDelayHours { get; set; } = 24;

    /// <summary>
    ///     Validates the section before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (!SupportedSlotCounts.Contains(MaxPerAccount))
        {
            throw new InvalidOperationException(
                $"ultima.characters.max_per_account must be 1, 5, 6 or 7, found {MaxPerAccount}."
            );
        }

        if (DeletionDelayHours < 1)
        {
            throw new InvalidOperationException(
                $"ultima.characters.deletion_delay_hours must be at least 1, found {DeletionDelayHours}."
            );
        }
    }
}
