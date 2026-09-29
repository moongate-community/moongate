namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for the item templates the server itself makes, for players and NPCs alike.
/// </summary>
public sealed class ItemsConfig
{
    /// <summary>
    ///     Gets or sets the item template of the backpack new characters and spawned NPCs wear.
    /// </summary>
    public string BackpackTemplate { get; set; } = "0x0e75_backpack";

    /// <summary>
    ///     Gets or sets the item template of gold coins.
    /// </summary>
    public string GoldTemplate { get; set; } = "0x0eed_gold_coin";

    /// <summary>
    ///     Validates the section before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BackpackTemplate) || string.IsNullOrWhiteSpace(GoldTemplate))
        {
            throw new InvalidOperationException("ultima.items.backpack_template and ultima.items.gold_template must be set.");
        }
    }
}
