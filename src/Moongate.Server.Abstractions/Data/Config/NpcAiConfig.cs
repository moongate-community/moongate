namespace Moongate.Server.Abstractions.Data.Config;

public sealed class NpcAiConfig
{
    public NpcAiAdvancedConfig Advanced { get; set; } = new();
}
