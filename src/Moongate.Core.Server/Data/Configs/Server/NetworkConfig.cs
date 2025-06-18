namespace Moongate.Core.Server.Data.Configs.Server;

public class NetworkConfig
{
    public int Port { get; set; } = 2593;

    public bool LogPacketsToFile { get; set; } = false;

    public bool LogPacketsToConsole { get; set; } = false;
}
