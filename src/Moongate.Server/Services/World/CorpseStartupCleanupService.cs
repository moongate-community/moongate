using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Constants;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Startup-only cleanup that removes persisted corpse items before runtime services begin.
/// </summary>
public sealed class CorpseStartupCleanupService : ICorpseStartupCleanupService
{
    private readonly ILogger _logger = Log.ForContext<CorpseStartupCleanupService>();
    private readonly IPersistenceService _persistenceService;

    public CorpseStartupCleanupService(IPersistenceService persistenceService)
    {
        ArgumentNullException.ThrowIfNull(persistenceService);

        _persistenceService = persistenceService;
    }

    public async Task StartAsync()
    {
        var itemRepository = _persistenceService.UnitOfWork.Items;
        var corpseRoots = await itemRepository.QueryAsync(
            static item => IsPersistedCorpse(item),
            static item => item
        );

        if (corpseRoots.Count == 0)
        {
            _logger.Debug("Corpse startup cleanup found no persisted corpses.");

            return;
        }

        foreach (var corpseRoot in corpseRoots)
        {
            await DetachPersistedOwnerReferencesAsync(corpseRoot);
        }

        var containmentRelations = await itemRepository.QueryAsync(
            static item => item.ParentContainerId != Serial.Zero,
            static item => (item.Id, item.ParentContainerId)
        );
        var childrenByParent = BuildChildrenLookup(containmentRelations);
        var removalOrder = BuildRemovalOrder(corpseRoots.Select(static corpse => corpse.Id).ToArray(), childrenByParent);
        var removedCount = 0;

        foreach (var itemId in removalOrder)
        {
            var removed = await itemRepository.RemoveAsync(itemId);

            if (!removed)
            {
                throw new InvalidOperationException(
                    $"Failed to remove persisted corpse item {itemId} during startup cleanup."
                );
            }

            removedCount++;
        }

        if (removedCount == 0)
        {
            _logger.Debug("Corpse startup cleanup found persisted corpses but removed no items.");

            return;
        }

        await _persistenceService.SaveAsync();
        _logger.Information(
            "Corpse startup cleanup removed {CorpseRootCount} corpse roots and {RemovedItemCount} total items.",
            corpseRoots.Count,
            removedCount
        );
    }

    public Task StopAsync()
        => Task.CompletedTask;

    private static Dictionary<Serial, List<Serial>> BuildChildrenLookup(
        IReadOnlyList<(Serial ItemId, Serial ParentContainerId)> containmentRelations
    )
    {
        var childrenByParent = new Dictionary<Serial, List<Serial>>();

        foreach (var (itemId, parentContainerId) in containmentRelations)
        {
            if (!childrenByParent.TryGetValue(parentContainerId, out var children))
            {
                children = [];
                childrenByParent[parentContainerId] = children;
            }

            children.Add(itemId);
        }

        return childrenByParent;
    }

    private static List<Serial> BuildRemovalOrder(
        IReadOnlyList<Serial> corpseRootIds,
        IReadOnlyDictionary<Serial, List<Serial>> childrenByParent
    )
    {
        var removalOrder = new List<Serial>();
        var visited = new HashSet<Serial>();

        foreach (var corpseRootId in corpseRootIds)
        {
            AppendRemovalOrderIterative(corpseRootId, childrenByParent, visited, removalOrder);
        }

        return removalOrder;
    }

    private static void AppendRemovalOrderIterative(
        Serial itemId,
        IReadOnlyDictionary<Serial, List<Serial>> childrenByParent,
        HashSet<Serial> visited,
        ICollection<Serial> removalOrder
    )
    {
        var traversalStack = new Stack<(Serial ItemId, bool IsPostOrder)>();
        traversalStack.Push((itemId, false));

        while (traversalStack.Count > 0)
        {
            var (currentItemId, isPostOrder) = traversalStack.Pop();

            if (isPostOrder)
            {
                removalOrder.Add(currentItemId);

                continue;
            }

            if (!visited.Add(currentItemId))
            {
                continue;
            }

            traversalStack.Push((currentItemId, true));

            if (!childrenByParent.TryGetValue(currentItemId, out var children))
            {
                continue;
            }

            for (var i = children.Count - 1; i >= 0; i--)
            {
                traversalStack.Push((children[i], false));
            }
        }
    }

    private async Task DetachPersistedOwnerReferencesAsync(UOItemEntity corpseRoot)
    {
        if (corpseRoot.ParentContainerId != Serial.Zero)
        {
            var parentContainer = await _persistenceService.UnitOfWork.Items.GetByIdAsync(corpseRoot.ParentContainerId);

            if (parentContainer is not null)
            {
                parentContainer.RemoveItem(corpseRoot.Id);
                await _persistenceService.UnitOfWork.Items.UpsertAsync(parentContainer);
            }
        }

        if (corpseRoot.EquippedMobileId == Serial.Zero || corpseRoot.EquippedLayer is null)
        {
            return;
        }

        var mobile = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(corpseRoot.EquippedMobileId);

        if (mobile is null)
        {
            return;
        }

        _ = mobile.UnequipItem(corpseRoot.EquippedLayer.Value, corpseRoot);
        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile);
    }

    private static bool IsPersistedCorpse(UOItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.ItemId == CorpsePropertyKeys.ItemId &&
               item.TryGetCustomBoolean(CorpsePropertyKeys.IsCorpse, out var isCorpse) &&
               isCorpse;
    }
}
