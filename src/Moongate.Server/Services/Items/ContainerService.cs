using Moongate.Server.Interfaces;
using Moongate.UO.Data.Containers;

namespace Moongate.Server.Services.Items;

/// <summary>
/// In-memory registry of container definitions, queryable by id. Populated at startup by
/// <see cref="Moongate.Server.Loaders.ContainersLoader" />.
/// </summary>
public sealed class ContainerService : IContainerService
{
    private readonly Dictionary<string, ContainerDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ContainerDefinition> All => [.. _byId.Values.OrderBy(container => container.Id)];

    public int Count => _byId.Count;

    public void Register(ContainerDefinition container)
    {
        _byId[container.Id] = container;
    }

    public ContainerDefinition? GetById(string id)
    {
        return _byId.GetValueOrDefault(id);
    }
}
