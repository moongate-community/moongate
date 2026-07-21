namespace Moongate.Server.Abstractions.Data.Config;

/// <summary>Root Moongate configuration section, loaded from moongate.yaml.</summary>
public sealed class MoongateConfig
{
    public string ShardName { get; set; } = "Moongate";

    public int StatsRefreshSeconds { get; set; } = 30;

    public string UltimaDirectory { get; set; }

    public MoongateNetworkConfig Network { get; set; } = new();
}
