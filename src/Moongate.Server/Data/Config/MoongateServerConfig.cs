using Moongate.Server.Data.Config.Sections;

namespace Moongate.Server.Data.Config;

public class MoongateServerConfig
{
    public ShardConfig Shard { get; set; } = new ShardConfig();

    public NetworkConfig Network { get; set; } = new NetworkConfig();

    public UltimaConfig Ultima { get; set; } = new UltimaConfig();
}
