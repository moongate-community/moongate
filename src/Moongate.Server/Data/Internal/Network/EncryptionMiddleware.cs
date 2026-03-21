using Moongate.Network.Client;
using Moongate.Network.Interfaces;
using Moongate.Server.Data.Session;

namespace Moongate.Server.Data.Internal.Network;

/// <summary>
/// Applies per-session transport encryption to inbound and outbound socket payloads.
/// </summary>
internal sealed class EncryptionMiddleware : INetMiddleware
{
    private readonly GameNetworkSession _session;

    public EncryptionMiddleware(GameNetworkSession session)
    {
        _session = session;
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTCPClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        _ = client;
        cancellationToken.ThrowIfCancellationRequested();

        if (data.IsEmpty || _session.Encryption is null)
        {
            return ValueTask.FromResult(data);
        }

        var buffer = data.ToArray();
        _session.Encryption.ClientDecrypt(buffer);

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(buffer);
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        MoongateTCPClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        _ = client;
        cancellationToken.ThrowIfCancellationRequested();

        if (data.IsEmpty || _session.Encryption is null)
        {
            return ValueTask.FromResult(data);
        }

        var buffer = data.ToArray();
        _session.Encryption.ServerEncrypt(buffer);

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(buffer);
    }
}
