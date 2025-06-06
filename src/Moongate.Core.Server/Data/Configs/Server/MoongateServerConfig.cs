namespace Moongate.Core.Server.Data.Configs.Server;

public class MoongateServerConfig
{
   public NetworkConfig Network { get; set; } = new();

   public WebServerConfig WebServer { get; set; } = new();

   public string Name { get; set; } = "Moongate Shard";
}
