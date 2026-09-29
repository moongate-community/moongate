namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for the live world.
/// </summary>
public sealed class WorldConfig
{
    /// <summary>
    ///     Gets or sets how far players see mobiles and items, in cells along X or Y; the client is told with 0xC8.
    /// </summary>
    public int ViewRange { get; set; } = 18;

    /// <summary>
    ///     Validates the section before server services begin startup: the range must be one the client supports, from
    ///     5 to 24, as ModernUO and POL allow.
    /// </summary>
    public void Validate()
    {
        if (ViewRange is < 5 or > 24)
        {
            throw new InvalidOperationException($"world.view_range must be from 5 to 24, found {ViewRange}.");
        }
    }
}
