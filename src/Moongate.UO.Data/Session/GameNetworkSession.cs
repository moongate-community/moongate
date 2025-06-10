using Moongate.Core.Network.Servers.Tcp;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Session;

public class GameNetworkSession : IDisposable
{
    public string SessionId { get; set; }
    public NetworkSessionFeatureType Features { get; set; } = NetworkSessionFeatureType.None;
    public NetworkSessionStateType State { get; set; } = NetworkSessionStateType.None;
    public MoongateTcpClient NetworkClient { get; set; }

    public void Dispose()
    {
        SessionId = null;
        Features = NetworkSessionFeatureType.None;
        State = NetworkSessionStateType.None;
        NetworkClient = null;
    }
}
