using System.Net;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Types.Sessions;

namespace Moongate.Server.Core.Data.Sessions;

public sealed class NetworkSession
{
    private readonly Lock _sync = new();

    private INetworkConnection? _client;
    private NetworkSessionState _state;
    private uint? _seed;
    private string? _clientVersion;

    public long SessionId { get; }

    public INetworkConnection? Client
    {
        get
        {
            lock (_sync)
            {
                return _client;
            }
        }
    }

    public string? RemoteEndPoint { get; }

    public string? LocalEndPoint { get; }

    public string? RemoteIpAddress { get; }

    public string? LocalIpAddress { get; }

    public NetworkSessionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public uint? Seed
    {
        get
        {
            lock (_sync)
            {
                return _seed;
            }
        }
    }

    public string? ClientVersion
    {
        get
        {
            lock (_sync)
            {
                return _clientVersion;
            }
        }
    }

    public NetworkSession(INetworkConnection client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var remoteEndPoint = client.RemoteEndPoint;
        var localEndPoint = client.LocalEndPoint;

        SessionId = client.SessionId;
        RemoteEndPoint = remoteEndPoint?.ToString();
        LocalEndPoint = localEndPoint?.ToString();
        RemoteIpAddress = (remoteEndPoint as IPEndPoint)?.Address.ToString();
        LocalIpAddress = (localEndPoint as IPEndPoint)?.Address.ToString();
        _client = client;
        _state = NetworkSessionState.AwaitingSeed;
    }

    public void DetachClient()
    {
        lock (_sync)
        {
            DetachClientUnsafe();
        }
    }

    public void SetClientVersion(string clientVersion)
    {
        lock (_sync)
        {
            ThrowIfDisconnected();
            ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
            _clientVersion = clientVersion;
        }
    }

    public void SetSeed(uint seed)
    {
        lock (_sync)
        {
            ThrowIfDisconnected();
            _seed = seed;
        }
    }

    public void SetState(NetworkSessionState state)
    {
        lock (_sync)
        {
            if (_state == NetworkSessionState.Disconnected && state == NetworkSessionState.Disconnected)
            {
                return;
            }

            ThrowIfDisconnected();

            if (!Enum.IsDefined(state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            if (state == NetworkSessionState.Disconnected)
            {
                DetachClientUnsafe();

                return;
            }

            _state = state;
        }
    }

    private void DetachClientUnsafe()
    {
        _client = null;
        _state = NetworkSessionState.Disconnected;
    }

    private void ThrowIfDisconnected()
    {
        if (_state == NetworkSessionState.Disconnected)
        {
            throw new InvalidOperationException("A disconnected network session cannot be changed.");
        }
    }
}
