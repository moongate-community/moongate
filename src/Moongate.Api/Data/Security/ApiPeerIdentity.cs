using System.Collections.Frozen;
namespace Moongate.Api.Data.Security;
/// <summary>A locally authenticated process identity and its immutable operation permissions.</summary>
public sealed class ApiPeerIdentity
{
    private readonly FrozenSet<ushort> _operations;
    /// <summary>Gets the configured identity; no identity is taken from request payloads.</summary>
    public string PeerId { get; }
    /// <summary>Creates an identity with a snapshot of its allowed operation identifiers.</summary>
    public ApiPeerIdentity(string peerId, IEnumerable<ushort> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(operations);
        PeerId = peerId;
        _operations = operations.ToFrozenSet();
        if (_operations.Contains(0)) { throw new ArgumentException("Operation zero is invalid.", nameof(operations)); }
    }
    /// <summary>Determines whether this process may call the specified operation.</summary>
    public bool CanInvoke(ushort operationId) => _operations.Contains(operationId);
}
