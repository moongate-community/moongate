using Moongate.Core.Network.Servers.Tcp;
using Moongate.UO.Data.Middlewares;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.UO.Data.Session;

public class GameSession : IDisposable
{
    public string SessionId { get; set; }
    public UOAccountEntity Account { get; set; }
    public int Seed { get; set; }
    public UOMobileEntity Mobile { get; set; }
    public int PingSequence { get; set; }

    public long MoveCredit { get; set; } = 0;

    public long MoveTime { get; set; }

    public byte MoveSequence { get; set; }

    public NetworkSessionFeatureType Features { get; private set; } = NetworkSessionFeatureType.None;
    public NetworkSessionStateType State { get; private set; } = NetworkSessionStateType.None;
    public MoongateTcpClient NetworkClient { get; set; }

    public void Dispose()
    {
        SessionId = null;
        Features = NetworkSessionFeatureType.None;
        State = NetworkSessionStateType.None;
        NetworkClient = null;
        Mobile = null;
        Account = null;
    }

    public void SetFeatures(NetworkSessionFeatureType features)
    {
        if (Features.HasFlag(NetworkSessionFeatureType.Compression) &&
            !features.HasFlag(NetworkSessionFeatureType.Compression))
        {
            Log.ForContext<GameSession>()
               .Debug(
                   "Session {SessionId} disabling compression middleware.",
                   SessionId
               );

            NetworkClient.RemoveMiddleware<CompressionMiddleware>();
        }

        if (!Features.HasFlag(NetworkSessionFeatureType.Compression) &&
            features.HasFlag(NetworkSessionFeatureType.Compression))
        {
            Log.ForContext<GameSession>()
               .Debug(
                   "Session {SessionId} enabling compression middleware.",
                   SessionId
               );
            NetworkClient.AddMiddleware(new CompressionMiddleware());
        }

        Features = features;
    }

    public void SetState(NetworkSessionStateType state)
    {
        if (State != state)
        {
            State = state;
        }
    }
}
