using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.UO.Data.Version;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles client version (0xBD): records the reported build on the session so later protocol
/// decisions can branch on it. Sent by the client in response to the server's version request.
/// </summary>
public sealed class ClientVersionHandler : IPacketHandler<ClientVersionPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<ClientVersionHandler>();

    public void Handle(ClientVersionPacket packet, in PacketContext context)
    {
        var rawVersion = packet.Version.Trim();

        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            _logger.Debug("Received empty client version from session {SessionId}", context.Session.SessionId);

            return;
        }

        var version = new ClientVersion(rawVersion);
        context.Session.SetVersion(version);

        _logger.Information(
            "Session {SessionId} reported client version {Version} ({Type})",
            context.Session.SessionId,
            version.SourceString,
            version.Type
        );
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
