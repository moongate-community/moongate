namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for what every new character gets besides the sets of <c>data/starting_items.toml</c>.
/// </summary>
public sealed class StartingItemsConfig
{
    /// <summary>
    ///     Gets or sets the item template of the backpack every new character wears.
    /// </summary>
    public string BackpackTemplate { get; set; } = "0x0e75_backpack";

    /// <summary>
    ///     Gets or sets the item template of the starting gold.
    /// </summary>
    public string GoldTemplate { get; set; } = "0x0eed_gold_coin";

    /// <summary>
    ///     Gets or sets how much gold goes in the backpack; 0 gives none.
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
        if (string.IsNullOrWhiteSpace(BackpackTemplate) || string.IsNullOrWhiteSpace(GoldTemplate))
        {
            throw new InvalidOperationException("starting_items.backpack_template and gold_template must be set.");
        }

        if (Gold < 0)
        {
            throw new InvalidOperationException($"starting_items.gold must be 0 or more, found {Gold}.");
        }

        if (BestSkills < 1)
        {
            throw new InvalidOperationException($"starting_items.best_skills must be at least 1, found {BestSkills}.");
        }
    }
}
