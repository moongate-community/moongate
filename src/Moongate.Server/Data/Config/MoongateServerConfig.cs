using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Serialization.Config.Internal;
using Tomlyn.Serialization;

namespace Moongate.Server.Data.Config;

public class MoongateServerConfig
{
    /// <summary>Gets or sets the configured server roles, defaulting to both login and game.</summary>
    /// <remarks>This setting does not yet change which services are started.</remarks>
    [TomlConverter(typeof(ServerModeTomlConverter))]
    public ServerMode Mode { get; set; } = ServerMode.Standalone;

    public ShardConfig Shard { get; set; } = new ShardConfig();

    public NetworkConfig Network { get; set; } = new NetworkConfig();

    public UltimaConfig Ultima { get; set; } = new UltimaConfig();

    public WorldSaveConfig WorldSave { get; set; } = new();

    public DiagnosticConfig Diagnostics { get; set; } = new();

    public ScriptingConfig Scripting { get; set; } = new();

    /// <summary>Validates configuration before server services begin startup.</summary>
    public void Validate()
    {
        if (Mode is not (ServerMode.Login or ServerMode.Game or ServerMode.Standalone))
        {
            throw new InvalidOperationException("The server mode must be Login, Game, or Standalone.");
        }

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

        if (Scripting is null)
        {
            throw new InvalidOperationException("The scripting configuration section cannot be null.");
        }
    }
}
