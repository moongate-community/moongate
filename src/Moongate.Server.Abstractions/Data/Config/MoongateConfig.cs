namespace Moongate.Server.Abstractions.Data.Config;

/// <summary>Root Moongate configuration section, loaded from moongate.yaml.</summary>
public sealed class MoongateConfig
{
    public string ShardName { get; set; } = "Moongate";

    public int StatsRefreshSeconds { get; set; } = 30;

    public string UltimaDirectory { get; set; }

    public MoongateNetworkConfig Network { get; set; } = new();

    /// <summary>
    /// When true, a world mutation attempted off the game-loop thread throws instead of warning.
    /// Off by default (a live shard warns rather than crashes); dev and CI turn it on so regressions
    /// fail loudly.
    /// </summary>
    public bool StrictLoopAffinity { get; set; } = false;
}
