using System.Collections.Frozen;
using Moongate.Core.Geometry;
using Moongate.Core.Random;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Serves the entries <see cref="Loaders.ContainersLoader" /> loaded, which has checked there is exactly one default.
/// </summary>
public class ContainerLayoutService : IContainerLayoutService
{
    private readonly Lazy<(FrozenDictionary<int, ContainerContent> ByItemId, ContainerContent Default)> _layouts;

    public ContainerLayoutService(IDataLoaderService dataLoaderService)
    {
        _layouts = new(
            () =>
            {
                var containers = dataLoaderService.GetEntities<ContainerContent>();

                return (containers.SelectMany(container => container.Items, (container, itemId) => (itemId, container))
                                  .ToFrozenDictionary(pair => pair.itemId, pair => pair.container),
                        containers.Single(container => container.Default));
            }
        );
    }

    public ContainerContent GetLayout(int itemId)
    {
        return _layouts.Value.ByItemId.GetValueOrDefault(itemId) ?? _layouts.Value.Default;
    }

    // Bounds hold the first corner and exclude the second.
    public Point2D RandomGridPosition(int itemId)
    {
        var bounds = GetLayout(itemId).Bounds;

        return new(Pick(bounds.Start.X, bounds.End.X), Pick(bounds.Start.Y, bounds.End.Y));
    }

    private static int Pick(int start, int end)
    {
        return end > start ? BuiltInRng.Next(start, end - start) : start;
    }
}
