using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Server.Data.Config.Sections;

public class NetworkConfig
{
    public int LoginPort { get; set; } = 2593;

    public int GamePort { get; set; } = 2595;

    public string ListenAddress { get; set; } = "0.0.0.0";

    public bool EnablePingServer { get; set; } = true;

    public void Validate(ServerMode mode)
    {
        if ((mode & ServerMode.Login) != 0 && LoginPort is < 0 or > 65535)
        {
            throw new InvalidOperationException("network.login_port must be between 0 and 65535.");
        }

        if ((mode & ServerMode.Game) != 0 && GamePort is < 0 or > 65535)
        {
            throw new InvalidOperationException("network.game_port must be between 0 and 65535.");
        }

        if (mode == ServerMode.Standalone && LoginPort == GamePort && LoginPort != 0)
        {
            throw new InvalidOperationException("network.login_port and network.game_port must differ in standalone mode.");
        }
    }
}
