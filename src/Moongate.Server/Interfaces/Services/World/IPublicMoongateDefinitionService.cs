using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Loads the shared shard-wide public moongate destination network.
/// </summary>
public interface IPublicMoongateDefinitionService
{
    /// <summary>
    /// Loads all public moongate groups and destinations from the Lua source of truth.
    /// </summary>
    /// <returns>Typed public moongate groups.</returns>
    IReadOnlyList<PublicMoongateGroupDefinition> Load();
}
