namespace Moongate.Server.Data.Config;

/// <summary>
/// Represents MoongateGameConfig.
/// </summary>
public class MoongateGameConfig
{
    public string ShardName { get; set; } = "Moongate Shard";

    public bool PingServerEnabled { get; set; } = true;

    public int PingServerPort { get; set; } = 12000;

    public int TimerTickMilliseconds { get; set; } = 250;

    public int TimerWheelSize { get; set; } = 512;

    public int CorpseDecaySeconds { get; set; } = 300;

    public bool IdleCpuEnabled { get; set; } = true;

    public int IdleSleepMilliseconds { get; set; } = 1;
}
