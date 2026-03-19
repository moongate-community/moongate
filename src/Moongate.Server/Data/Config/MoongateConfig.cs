using System.Text.Json.Serialization;
using Moongate.Core.Types;

namespace Moongate.Server.Data.Config;

/// <summary>
/// Represents MoongateConfig.
/// </summary>
public class MoongateConfig
{
    [JsonIgnore]
    public string RootDirectory { get; set; }

    public string UODirectory { get; set; }

    public LogLevelType LogLevel { get; set; }

    public bool LogPacketData { get; set; }

    public bool IsDeveloperMode { get; set; }

    public MoongateHttpConfig Http { get; set; } = new();

    public MoongateGameConfig Game { get; set; } = new();

    public MoongateMetricsConfig Metrics { get; set; } = new();

    public MoongatePersistenceConfig Persistence { get; set; } = new();

    public MoongateSpatialConfig Spatial { get; set; } = new();

    public MoongateScriptingConfig Scripting { get; set; } = new();

    public MoongateLlmConfig Llm { get; set; } = new();

    public MoongateEmailConfig Email { get; set; } = new();
}
