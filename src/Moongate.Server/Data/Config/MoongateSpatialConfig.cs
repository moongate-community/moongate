namespace Moongate.Server.Data.Config;

/// <summary>
/// Configures spatial index lazy-loading and warmup behavior.
/// </summary>
public sealed class MoongateSpatialConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether lazy loading of ground items is enabled.
    /// </summary>
    public bool LazySectorItemLoadEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the warmup radius in sectors around the player login sector.
    /// A value of <c>1</c> warms a 3x3 block around the center sector.
    /// </summary>
    public int SectorWarmupRadius { get; set; } = 1;
}
