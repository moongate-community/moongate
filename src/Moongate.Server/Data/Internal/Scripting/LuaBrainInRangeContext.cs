using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Deferred in-range notification for a brain runtime state.
/// </summary>
public readonly record struct LuaBrainInRangeContext(
    Serial SourceMobileId,
    Dictionary<string, object> Payload
);
