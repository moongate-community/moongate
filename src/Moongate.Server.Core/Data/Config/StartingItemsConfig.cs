namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for what every new character gets besides the sets of <c>data/starting_items.toml</c>; the
///     backpack and gold templates are in <see cref="ItemsConfig" />.
/// </summary>
public sealed class StartingItemsConfig
{
    /// <summary>
    ///     Gets or sets how much gold goes in the backpack, up to 65535 (one pile); 0 gives none.
    /// </summary>
    public int Gold { get; set; } = 1000;

    /// <summary>
    ///     Gets or sets how many of the character's highest skills pick skill sets.
    /// </summary>
    public int BestSkills { get; set; } = 3;

    /// <summary>
    ///     Validates the section before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (Gold is < 0 or > ushort.MaxValue)
        {
            throw new InvalidOperationException($"starting_items.gold must be from 0 to 65535, found {Gold}.");
        }

        if (BestSkills < 1)
        {
            throw new InvalidOperationException($"starting_items.best_skills must be at least 1, found {BestSkills}.");
        }
    }
}
