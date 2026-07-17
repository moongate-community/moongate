using Moongate.UO.Data.Containers;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>In-memory registry of container definitions, queryable by id.</summary>
public interface IContainerService
{
    /// <summary>All registered container definitions, ordered by id.</summary>
    IReadOnlyList<ContainerDefinition> All { get; }

    /// <summary>Number of registered container definitions.</summary>
    int Count { get; }

    /// <summary>Returns the container definition with the given id (case-insensitive), or null.</summary>
    ContainerDefinition? GetById(string id);

    /// <summary>Adds or replaces a container definition, indexed by id.</summary>
    void Register(ContainerDefinition container);
}
