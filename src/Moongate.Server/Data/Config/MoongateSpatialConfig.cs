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

    /// <summary>
    /// Gets or sets the sector radius used to send item/mobile snapshots when a player enters a new sector.
    /// A value of <c>3</c> sends snapshots for a 7x7 sector area centered on the entered sector.
    /// </summary>
    public int SectorEnterSyncRadius { get; set; } = 3;

    /// <summary>
    /// Gets or sets the lazy-load radius (in sectors) used when a sector is accessed.
    /// A value of <c>3</c> loads a 7x7 area centered on the requested sector.
    /// </summary>
    public int LazySectorEntityLoadRadius { get; set; } = 3;

    /// <summary>
    /// Gets or sets the sector radius used for live update broadcast (item/mobile changes).
    /// A value of <c>3</c> sends updates to a 7x7 sector area centered on source sector.
    /// </summary>
    public int SectorUpdateBroadcastRadius { get; set; } = 3;
}
