namespace Moongate.Server.Data.Config.Sections;

/// <summary>Represents either an explicit operation list or the all-operations wildcard.</summary>
public sealed class ApiOperationPermissionsConfig
{
    public IReadOnlyList<ushort> OperationIds { get; }
    public bool AllowsAll { get; }

    public ApiOperationPermissionsConfig(IEnumerable<ushort> operationIds, bool allowsAll = false)
    {
        ArgumentNullException.ThrowIfNull(operationIds);
        OperationIds = Array.AsReadOnly(operationIds.ToArray());
        AllowsAll = allowsAll;
    }

    /// <summary>Rejects reserved identifiers and ambiguous wildcard/list combinations.</summary>
    public void Validate()
    {
        if (OperationIds.Contains((ushort)0) || (AllowsAll && OperationIds.Count != 0))
        {
            throw new InvalidOperationException("api.peers.allowed_operations must be a list of IDs from 1 to 65535 or [\"*\"] alone.");
        }
    }
}
