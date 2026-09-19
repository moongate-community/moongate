using System.Security.Authentication;
using Moongate.Api.Data.Security;
using Moongate.Network.Client;
using Moongate.Network.Data;
using Moongate.Network.Interfaces.Framing;

namespace Moongate.Api.Security.Internal;

internal sealed class ApiConnectionSetup
{
    private readonly ApiTlsPolicy _policy;
    private readonly Action<MoongateTcpClient, ApiPeerIdentity> _configure;
    private ApiPeerIdentity? _peer;
    private int _used;
    public ConnectionPipeline Pipeline { get; }

    public ApiConnectionSetup(ApiTlsPolicy policy, INetFramer framer, Action<MoongateTcpClient, ApiPeerIdentity> configure, string? targetHost = null, string? expectedPeerId = null)
    {
        _policy = policy;
        _configure = configure;
        if ((targetHost is null) != (expectedPeerId is null)) { throw new ArgumentException("Outbound setup requires both target host and expected peer identity."); }
        Pipeline = new ConnectionPipeline(framer: framer)
        {
            PrepareStreamAsync = (stream, token) => PrepareAsync(stream, targetHost, expectedPeerId, token),
            ConfigureClient = Configure
        };
    }

    private ValueTask<Stream> PrepareAsync(Stream stream, string? targetHost, string? expectedPeerId, CancellationToken token)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0) { throw new InvalidOperationException("Connection setup cannot be reused."); }
        return targetHost is null
            ? _policy.PrepareServerAsync(stream, peer => _peer = peer, token)
            : _policy.PrepareClientAsync(stream, targetHost, expectedPeerId!, peer => _peer = peer, token);
    }

    private void Configure(MoongateTcpClient client)
    {
        if (_peer is null) { throw new AuthenticationException("A connection must authenticate before callbacks are installed."); }
        _configure(client, _peer);
    }
}
