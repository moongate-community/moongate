using Moongate.Server.Admin.Data.Config;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Serialization.Config.Internal;
using Tomlyn.Serialization;

namespace Moongate.Server.Data.Config;

public class MoongateServerConfig
{
    /// <summary>
    ///     Gets or sets the configured server roles, defaulting to both login and game.
    /// </summary>
    [TomlConverter(typeof(ServerModeTomlConverter))]
    public ServerMode Mode { get; set; } = ServerMode.Standalone;

    public ShardConfig Shard { get; set; } = new();

    public NetworkConfig Network { get; set; } = new();

    public AdminApiConfig AdminApi { get; set; } = new();

    public RedisConfig Redis { get; set; } = new();

    public UltimaConfig Ultima { get; set; } = new();

    public PersistenceConfig Persistence { get; set; } = new();

    public RealmDirectoryConfig RealmDirectory { get; set; } = new();

    public WorldSaveConfig WorldSave { get; set; } = new();

    public DiagnosticConfig Diagnostics { get; set; } = new();

    public ScriptingConfig Scripting { get; set; } = new();

    /// <summary>
    ///     Validates configuration before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (Mode is not (ServerMode.Login or ServerMode.Game or ServerMode.Standalone))
        {
            throw new InvalidOperationException("The server mode must be Login, Game, or Standalone.");
        }

        if (Network is null)
        {
            throw new InvalidOperationException("The network configuration section cannot be null.");
        }

        Network.Validate(Mode);

        if (Redis is null)
        {
            throw new InvalidOperationException("The redis configuration section cannot be null.");
        }

        Redis.Validate();

        if (AdminApi is null)
        {
            throw new InvalidOperationException("The admin_api configuration section cannot be null.");
        }

        AdminApi.Validate();

        if (Persistence is null)
        {
            throw new InvalidOperationException("The persistence configuration section cannot be null.");
        }

        Persistence.Validate(Mode);

        if (RealmDirectory is null)
        {
            throw new InvalidOperationException("The realm_directory configuration section cannot be null.");
        }

        RealmDirectory.Validate(Mode);

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

        Scripting.Validate();
    }
}
