namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     The <c>[ultima.starting_items]</c> settings for choosing the sets of <c>data/starting_items.toml</c>, which also
///     give the starting gold; the backpack template is in <see cref="ItemsConfig" />.
/// </summary>
public sealed class StartingItemsConfig
{
    /// <summary>
    ///     Gets or sets how many of the character's highest skills pick skill sets.
    /// </summary>
    public int BestSkills { get; set; } = 3;

    /// <summary>
    ///     Validates the section before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (BestSkills < 1)
        {
            throw new InvalidOperationException(
                $"ultima.starting_items.best_skills must be at least 1, found {BestSkills}."
            );
        }
    }
}
