using Moongate.Api.Data.Security;
using Moongate.Api.Interfaces.Connections;
namespace Moongate.Api.Data.Requests;
/// <summary>Provides trusted connection identity and correlation for an incoming operation.</summary>
public sealed class ApiRequestContext
{
    /// <summary>Gets the authenticated connection.</summary>
    public IApiConnection Connection { get; }
    /// <summary>Gets the verified calling process.</summary>
    public ApiPeerIdentity Peer => Connection.Peer;
    /// <summary>Gets the peer's request identifier.</summary>
    public uint RequestId { get; }
    /// <summary>Gets the registered operation identifier.</summary>
    public ushort OperationId { get; }
    /// <summary>Creates a context from an authenticated connection and a decoded request.</summary>
    public ApiRequestContext(IApiConnection connection, uint requestId, ushort operationId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connection = connection;
        RequestId = requestId;
        OperationId = operationId;
    }
}
