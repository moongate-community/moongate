using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Resolves coarse-grained AI relationships between two mobiles.
/// </summary>
public interface IAiRelationService
{
    /// <summary>
    /// Computes the AI relation that <paramref name="viewer" /> should apply to <paramref name="target" />.
    /// </summary>
    AiRelation Compute(UOMobileEntity viewer, UOMobileEntity target);
}
