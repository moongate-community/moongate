using Moongate.Server.Data.Config.Sections;

namespace Moongate.Server.Data.Config;

public class MoongateServerConfig
{
    public ShardConfig Shard { get; set; } = new ShardConfig();

    public NetworkConfig Network { get; set; } = new NetworkConfig();

    public UltimaConfig Ultima { get; set; } = new UltimaConfig();

    public WorldSaveConfig WorldSave { get; set; } = new();

    public DiagnosticConfig Diagnostics { get; set; } = new();

    /// <summary>Validates configuration before server services begin startup.</summary>
    public void Validate()
    {
        if (WorldSave is null)
        {
            throw new InvalidOperationException("The world_save configuration section cannot be null.");
        }
        WorldSave.Validate();

        if (Diagnostics is null)
        {
            throw new InvalidOperationException("The diagnostics configuration section cannot be null.");
        }
        Diagnostics.ToOptions();
    }
}
