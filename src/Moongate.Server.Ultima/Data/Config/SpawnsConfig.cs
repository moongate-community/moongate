namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for the spawn regions.
/// </summary>
public sealed class SpawnsConfig
{
    /// <summary>
    ///     Gets or sets whether the first spawn of each region after the start fills it to its max at once, so an empty
    ///     world is full within minutes; later spawns then follow the region's call and times. False keeps UOX3's
    ///     behaviour, where a region with a call of 1 can take hours to fill.
    /// </summary>
    public bool InitialFill { get; set; } = true;
}
