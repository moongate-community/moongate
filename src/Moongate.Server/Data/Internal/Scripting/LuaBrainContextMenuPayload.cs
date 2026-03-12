using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Context payload used for Lua brain context menu hooks.
/// </summary>
public readonly record struct LuaBrainContextMenuPayload(
    Serial TargetMobileId,
    UOMobileEntity? Requester,
    long SessionId,
    string? MenuKey
);
