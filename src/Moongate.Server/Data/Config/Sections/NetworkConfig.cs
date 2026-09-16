namespace Moongate.Server.Data.Config.Sections;

public class NetworkConfig
{
    public int GamePort { get; set; } = 2593;

    public string ListenAddress { get; set; } = "0.0.0.0";

    public bool EnablePingServer { get; set; } = true;

}
