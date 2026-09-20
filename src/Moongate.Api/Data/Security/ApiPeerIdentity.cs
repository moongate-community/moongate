using System.Collections.Frozen;

namespace Moongate.Api.Data.Security;

/// <summary>A locally authenticated process identity and its immutable operation permissions.</summary>
public sealed class ApiPeerIdentity
{
    private readonly FrozenSet<ushort> _operations;

    /// <summary>Gets the configured identity; no identity is taken from request payloads.</summary>
    public string PeerId { get; }

    /// <summary>Gets whether every nonzero operation identifier is permitted, including future operations.</summary>
    public bool AllowsAllOperations { get; }

    /// <summary>Creates an identity with a snapshot of its allowed operation identifiers. An empty list denies all operations.</summary>
    public ApiPeerIdentity(string peerId, IEnumerable<ushort> operations)
        : this(peerId, operations, false)
    {
    }

    /// <summary>Creates an identity with explicit permissions or access to every nonzero operation.</summary>
    /// <param name="peerId">The locally configured process identity.</param>
    /// <param name="operations">Operation identifiers to snapshot; zero is always invalid.</param>
    /// <param name="allowAllOperations">Whether all nonzero operations, including future registrations, are allowed.</param>
    public ApiPeerIdentity(string peerId, IEnumerable<ushort> operations, bool allowAllOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(operations);
        PeerId = peerId;
        AllowsAllOperations = allowAllOperations;
        _operations = operations.ToFrozenSet();

        if (_operations.Contains(0)) { throw new ArgumentException("Operation zero is invalid.", nameof(operations)); }
    }

    /// <summary>Determines whether this process may call the specified operation.</summary>
    public bool CanInvoke(ushort operationId)
        => operationId != 0 && (AllowsAllOperations || _operations.Contains(operationId));
}
