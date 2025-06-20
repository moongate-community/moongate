using System.ComponentModel;
using Moongate.Core.Network.Servers.Tcp;
using Moongate.UO.Data.Middlewares;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.UO.Data.Session;

public class GameSession : IDisposable, INotifyPropertyChanged
{
    public delegate void ObjectChanged<in TEntity>(object sender, TEntity entity) where TEntity : class;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event ObjectChanged<UOMobileEntity> MobileChanged;

    public string SessionId { get; set; }
    public UOAccountEntity Account { get; set; }
    public int Seed { get; set; }
    public UOMobileEntity Mobile { get; set; }
    public int PingSequence { get; set; }

    public NetworkSessionFeatureType Features { get; private set; } = NetworkSessionFeatureType.None;
    public NetworkSessionStateType State { get; private set; } = NetworkSessionStateType.None;
    public MoongateTcpClient NetworkClient { get; set; }


    public GameSession()
    {
        PropertyChanged += OnInternalPropertyChanged;
    }

    private void OnInternalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Mobile))
        {
            MobileChanged?.Invoke(this, Mobile);
        }
    }

    public void SetState(NetworkSessionStateType state)
    {
        if (State != state)
        {
            State = state;
        }
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


    public void Dispose()
    {
        SessionId = null;
        Features = NetworkSessionFeatureType.None;
        State = NetworkSessionStateType.None;
        NetworkClient = null;
        Mobile = null;
        Account = null;
        PropertyChanged -= OnInternalPropertyChanged;
    }
}
