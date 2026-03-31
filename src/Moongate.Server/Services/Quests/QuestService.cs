using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Quests;

/// <summary>
/// Coordinates player quest progress against static quest templates.
/// </summary>
public sealed class QuestService : IQuestService
{
    private readonly IQuestTemplateService _questTemplateService;
    private readonly IMobileService _mobileService;
    private readonly ICharacterService _characterService;
    private readonly IItemService _itemService;
    private readonly IItemFactoryService _itemFactoryService;

    public QuestService(
        IQuestTemplateService questTemplateService,
        IMobileService mobileService,
        ICharacterService characterService,
        IItemService itemService,
        IItemFactoryService itemFactoryService
    )
    {
        ArgumentNullException.ThrowIfNull(questTemplateService);
        ArgumentNullException.ThrowIfNull(mobileService);
        ArgumentNullException.ThrowIfNull(characterService);
        ArgumentNullException.ThrowIfNull(itemService);
        ArgumentNullException.ThrowIfNull(itemFactoryService);

        _questTemplateService = questTemplateService;
        _mobileService = mobileService;
        _characterService = characterService;
        _itemService = itemService;
        _itemFactoryService = itemFactoryService;
    }

    public Task<IReadOnlyList<QuestTemplateDefinition>> GetAvailableForNpcAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(npc);
        cancellationToken.ThrowIfCancellationRequested();

        var npcTemplateId = GetMobileTemplateId(npc);

        if (string.IsNullOrWhiteSpace(npcTemplateId))
        {
            return Task.FromResult<IReadOnlyList<QuestTemplateDefinition>>([]);
        }

        var available = _questTemplateService
                        .GetAll()
                        .Where(quest => MatchesTemplateId(quest.QuestGiverTemplateIds, npcTemplateId))
                        .Where(quest => CanAcceptQuest(player, quest))
                        .ToList();

        return Task.FromResult<IReadOnlyList<QuestTemplateDefinition>>(available);
    }

    public async Task<IReadOnlyList<QuestProgressEntity>> GetActiveForNpcAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(npc);
        cancellationToken.ThrowIfCancellationRequested();

        var npcTemplateId = GetMobileTemplateId(npc);

        if (string.IsNullOrWhiteSpace(npcTemplateId))
        {
            return [];
        }

        var active = player.QuestProgress
                           .Where(IsInJournal)
                           .Where(progress => TryGetQuest(progress.QuestId, out var quest) &&
                                              (MatchesTemplateId(quest.QuestGiverTemplateIds, npcTemplateId) ||
                                               MatchesTemplateId(quest.CompletionNpcTemplateIds, npcTemplateId)))
                           .ToList();

        var changed = false;

        foreach (var progress in active)
        {
            if (TryGetQuest(progress.QuestId, out var quest))
            {
                changed |= EnsureObjectiveProgress(progress, quest);
            }
        }

        if (changed)
        {
            await _mobileService.CreateOrUpdateAsync(player, cancellationToken);
        }

        return active;
    }

    public async Task<IReadOnlyList<QuestProgressEntity>> GetJournalAsync(
        UOMobileEntity player,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        cancellationToken.ThrowIfCancellationRequested();

        var active = player.QuestProgress.Where(IsInJournal).ToList();

        var changed = false;

        foreach (var progress in active)
        {
            if (TryGetQuest(progress.QuestId, out var quest))
            {
                changed |= EnsureObjectiveProgress(progress, quest);
            }
        }

        if (changed)
        {
            await _mobileService.CreateOrUpdateAsync(player, cancellationToken);
        }

        return active;
    }

    public async Task<bool> AcceptAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        string questId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentException.ThrowIfNullOrWhiteSpace(questId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetQuest(questId, out var quest))
        {
            return false;
        }

        var npcTemplateId = GetMobileTemplateId(npc);

        if (string.IsNullOrWhiteSpace(npcTemplateId) ||
            !MatchesTemplateId(quest.QuestGiverTemplateIds, npcTemplateId) ||
            !CanAcceptQuest(player, quest))
        {
            return false;
        }

        var progress = new QuestProgressEntity
        {
            QuestId = quest.Id,
            Status = QuestProgressStatusType.Active,
            AcceptedAtUtc = DateTime.UtcNow,
            Objectives = CreateObjectiveProgress(quest)
        };

        player.QuestProgress.Add(progress);

        var backpack = await _characterService.GetBackpackWithItemsAsync(player);
        EvaluateInventoryObjectives(progress, quest, backpack);
        await _mobileService.CreateOrUpdateAsync(player, cancellationToken);

        return true;
    }

    public async Task<bool> TryCompleteAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        string questId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentException.ThrowIfNullOrWhiteSpace(questId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetQuest(questId, out var quest))
        {
            return false;
        }

        var npcTemplateId = GetMobileTemplateId(npc);
        var progress = FindCurrentProgress(player, questId);

        if (string.IsNullOrWhiteSpace(npcTemplateId) ||
            progress is null ||
            !MatchesTemplateId(quest.CompletionNpcTemplateIds, npcTemplateId))
        {
            return false;
        }

        var backpack = await _characterService.GetBackpackWithItemsAsync(player);
        var progressChanged = EvaluateInventoryObjectives(progress, quest, backpack);

        if (progress.Status != QuestProgressStatusType.ReadyToTurnIn)
        {
            if (progressChanged)
            {
                await _mobileService.CreateOrUpdateAsync(player, cancellationToken);
            }

            return false;
        }

        if (!ValidateRewardTemplates(quest))
        {
            return false;
        }

        if (!await ConsumeDeliverObjectivesAsync(quest, backpack, cancellationToken))
        {
            return false;
        }

        if (!await GrantRewardsAsync(quest, backpack, cancellationToken))
        {
            return false;
        }

        progress.Status = QuestProgressStatusType.Completed;
        progress.CompletedAtUtc = DateTime.UtcNow;
        await _mobileService.CreateOrUpdateAsync(player, cancellationToken);

        return true;
    }

    public async Task OnMobileKilledAsync(
        UOMobileEntity player,
        UOMobileEntity killedMobile,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(killedMobile);
        cancellationToken.ThrowIfCancellationRequested();

        var killedTemplateId = GetMobileTemplateId(killedMobile);

        if (string.IsNullOrWhiteSpace(killedTemplateId))
        {
            return;
        }

        var changed = false;

        foreach (var progress in player.QuestProgress.Where(IsInJournal))
        {
            if (!TryGetQuest(progress.QuestId, out var quest))
            {
                continue;
            }

            changed |= EnsureObjectiveProgress(progress, quest);

            for (var index = 0; index < quest.Objectives.Count; index++)
            {
                var objective = quest.Objectives[index];

                if (objective.Type != QuestObjectiveType.Kill ||
                    !MatchesTemplateId(objective.MobileTemplateIds, killedTemplateId))
                {
                    continue;
                }

                var objectiveProgress = progress.Objectives[index];

                if (objectiveProgress.IsCompleted)
                {
                    continue;
                }

                objectiveProgress.CurrentAmount = Math.Min(objective.Amount, objectiveProgress.CurrentAmount + 1);
                objectiveProgress.IsCompleted = objectiveProgress.CurrentAmount >= objective.Amount;
                changed = true;
            }

            UpdateQuestStatus(progress);
        }

        if (changed)
        {
            await _mobileService.CreateOrUpdateAsync(player, cancellationToken);
        }
    }

    public async Task ReevaluateInventoryAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        cancellationToken.ThrowIfCancellationRequested();

        var backpack = await _characterService.GetBackpackWithItemsAsync(player);
        var changed = false;

        foreach (var progress in player.QuestProgress.Where(IsInJournal))
        {
            if (!TryGetQuest(progress.QuestId, out var quest))
            {
                continue;
            }

            changed |= EvaluateInventoryObjectives(progress, quest, backpack);
        }

        if (changed)
        {
            await _mobileService.CreateOrUpdateAsync(player, cancellationToken);
        }
    }

    private bool CanAcceptQuest(UOMobileEntity player, QuestTemplateDefinition quest)
    {
        if (player.QuestProgress.Any(progress => string.Equals(progress.QuestId, quest.Id, StringComparison.OrdinalIgnoreCase) &&
                                                progress.Status != QuestProgressStatusType.Completed))
        {
            return false;
        }

        if (!quest.Repeatable &&
            player.QuestProgress.Any(progress => string.Equals(progress.QuestId, quest.Id, StringComparison.OrdinalIgnoreCase) &&
                                                progress.Status == QuestProgressStatusType.Completed))
        {
            return false;
        }

        var activeCount = player.QuestProgress.Count(
            progress => string.Equals(progress.QuestId, quest.Id, StringComparison.OrdinalIgnoreCase) &&
                        IsInJournal(progress)
        );

        return activeCount < quest.MaxActivePerCharacter;
    }

    private async Task<bool> ConsumeDeliverObjectivesAsync(
        QuestTemplateDefinition quest,
        UOItemEntity? backpack,
        CancellationToken cancellationToken
    )
    {
        var deliverRequirements = quest.Objectives
                                     .Where(static objective => objective.Type == QuestObjectiveType.Deliver)
                                     .Where(static objective => !string.IsNullOrWhiteSpace(objective.ItemTemplateId))
                                     .GroupBy(static objective => objective.ItemTemplateId!, StringComparer.OrdinalIgnoreCase)
                                     .Select(static group => new { ItemTemplateId = group.Key, Amount = group.Sum(static objective => objective.Amount) })
                                     .ToList();

        if (deliverRequirements.Count == 0)
        {
            return true;
        }

        if (backpack is null)
        {
            return false;
        }

        foreach (var requirement in deliverRequirements)
        {
            if (CountItemAmount(backpack, requirement.ItemTemplateId) < requirement.Amount)
            {
                return false;
            }
        }

        foreach (var requirement in deliverRequirements)
        {
            await ConsumeItemTemplateAmountAsync(backpack, requirement.ItemTemplateId, requirement.Amount, cancellationToken);
        }

        return true;
    }

    private static List<QuestObjectiveProgressEntity> CreateObjectiveProgress(QuestTemplateDefinition quest)
        => quest.Objectives
                .Select(
                    static (objective, index) => new QuestObjectiveProgressEntity
                    {
                        ObjectiveIndex = index,
                        ObjectiveId = objective.ObjectiveId,
                        CurrentAmount = 0,
                        IsCompleted = false
                    }
                )
                .ToList();

    private async Task<bool> CreditGoldAsync(int amount, UOItemEntity? backpack, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (amount <= 0)
        {
            return true;
        }

        if (backpack is null)
        {
            return false;
        }

        var goldStack = FindFirstByTemplateId(backpack, "gold");

        if (goldStack is not null)
        {
            goldStack.Amount += amount;
            await _itemService.UpsertItemAsync(goldStack);

            return true;
        }

        if (!_itemFactoryService.TryGetItemTemplate("gold", out _))
        {
            return false;
        }

        var goldItem = _itemFactoryService.CreateItemFromTemplate("gold");
        goldItem.Amount = amount;
        backpack.AddItem(goldItem, new(1, 1));
        await _itemService.CreateItemAsync(goldItem);

        return true;
    }

    private static int CountItemAmount(UOItemEntity? container, string itemTemplateId)
    {
        if (container is null)
        {
            return 0;
        }

        var total = 0;

        foreach (var item in EnumerateItemsRecursive(container))
        {
            if (TryGetItemTemplateId(item, out var candidate) &&
                string.Equals(candidate, itemTemplateId, StringComparison.OrdinalIgnoreCase))
            {
                total += Math.Max(1, item.Amount);
            }
        }

        return total;
    }

    private async Task ConsumeItemTemplateAmountAsync(
        UOItemEntity container,
        string itemTemplateId,
        int amount,
        CancellationToken cancellationToken
    )
    {
        var remaining = amount;
        var matches = EnumerateItemsRecursiveWithParent(container)
                     .Where(
                         match => TryGetItemTemplateId(match.Item, out var candidate) &&
                                  string.Equals(candidate, itemTemplateId, StringComparison.OrdinalIgnoreCase)
                     )
                     .ToList();

        foreach (var match in matches)
        {
            if (remaining <= 0)
            {
                break;
            }

            var stack = match.Item;
            var consume = Math.Min(remaining, Math.Max(1, stack.Amount));
            remaining -= consume;

            if (consume >= stack.Amount)
            {
                match.Parent.RemoveItem(stack.Id);
                _ = await _itemService.DeleteItemAsync(stack.Id);
                continue;
            }

            stack.Amount -= consume;
            await _itemService.UpsertItemAsync(stack);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool EvaluateInventoryObjectives(
        QuestProgressEntity progress,
        QuestTemplateDefinition quest,
        UOItemEntity? backpack
    )
    {
        var changed = EnsureObjectiveProgress(progress, quest);

        for (var index = 0; index < quest.Objectives.Count; index++)
        {
            var objective = quest.Objectives[index];

            if (objective.Type != QuestObjectiveType.Collect && objective.Type != QuestObjectiveType.Deliver)
            {
                continue;
            }

            var objectiveProgress = progress.Objectives[index];
            var targetAmount = CountItemAmount(backpack, objective.ItemTemplateId ?? string.Empty);
            var newAmount = Math.Min(objective.Amount, targetAmount);
            var newIsCompleted = targetAmount >= objective.Amount;

            if (objectiveProgress.CurrentAmount != newAmount || objectiveProgress.IsCompleted != newIsCompleted)
            {
                objectiveProgress.CurrentAmount = newAmount;
                objectiveProgress.IsCompleted = newIsCompleted;
                changed = true;
            }
        }

        var previousStatus = progress.Status;
        UpdateQuestStatus(progress);

        return changed || previousStatus != progress.Status;
    }

    private QuestProgressEntity? FindCurrentProgress(UOMobileEntity player, string questId)
        => player.QuestProgress.FirstOrDefault(progress => string.Equals(progress.QuestId, questId, StringComparison.OrdinalIgnoreCase) &&
                                                          progress.Status != QuestProgressStatusType.Completed);

    private static UOItemEntity? FindFirstByTemplateId(UOItemEntity container, string itemTemplateId)
        => EnumerateItemsRecursive(container)
           .FirstOrDefault(
               item => TryGetItemTemplateId(item, out var candidate) &&
                       string.Equals(candidate, itemTemplateId, StringComparison.OrdinalIgnoreCase)
           );

    private static string? GetMobileTemplateId(UOMobileEntity mobile)
        => mobile.TryGetCustomString(MobileCustomParamKeys.Template.TemplateId, out var templateId) &&
           !string.IsNullOrWhiteSpace(templateId)
               ? templateId.Trim()
               : null;

    private async Task<bool> GrantRewardsAsync(QuestTemplateDefinition quest, UOItemEntity? backpack, CancellationToken cancellationToken)
    {
        foreach (var reward in quest.Rewards)
        {
            if (!await CreditGoldAsync(reward.Gold, backpack, cancellationToken))
            {
                return false;
            }

            foreach (var rewardItem in reward.Items)
            {
                if (!await GrantRewardItemAsync(rewardItem, backpack, cancellationToken))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task<bool> GrantRewardItemAsync(
        QuestRewardItemDefinition rewardItem,
        UOItemEntity? backpack,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backpack is null || rewardItem.Amount <= 0)
        {
            return false;
        }

        var item = _itemFactoryService.CreateItemFromTemplate(rewardItem.ItemTemplateId);

        if (item.IsStackable || rewardItem.Amount == 1)
        {
            item.Amount = rewardItem.Amount;
            backpack.AddItem(item, new(1, 1));
            await _itemService.CreateItemAsync(item);

            return true;
        }

        backpack.AddItem(item, new(1, 1));
        await _itemService.CreateItemAsync(item);

        for (var index = 1; index < rewardItem.Amount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extraItem = _itemFactoryService.CreateItemFromTemplate(rewardItem.ItemTemplateId);
            backpack.AddItem(extraItem, new(1, 1));
            await _itemService.CreateItemAsync(extraItem);
        }

        return true;
    }

    private static bool IsInJournal(QuestProgressEntity progress)
        => progress.Status == QuestProgressStatusType.Active || progress.Status == QuestProgressStatusType.ReadyToTurnIn;

    private static bool MatchesTemplateId(IEnumerable<string> templateIds, string candidate)
        => templateIds.Any(templateId => string.Equals(templateId, candidate, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<UOItemEntity> EnumerateItemsRecursive(UOItemEntity container)
    {
        foreach (var item in container.Items)
        {
            yield return item;

            foreach (var child in EnumerateItemsRecursive(item))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<(UOItemEntity Parent, UOItemEntity Item)> EnumerateItemsRecursiveWithParent(UOItemEntity container)
    {
        foreach (var item in container.Items)
        {
            yield return (container, item);

            foreach (var child in EnumerateItemsRecursiveWithParent(item))
            {
                yield return child;
            }
        }
    }

    private bool EnsureObjectiveProgress(QuestProgressEntity progress, QuestTemplateDefinition quest)
    {
        var normalized = new List<QuestObjectiveProgressEntity>(quest.Objectives.Count);
        var existingByObjectiveId = new Dictionary<string, Queue<QuestObjectiveProgressEntity>>(StringComparer.OrdinalIgnoreCase);
        var legacyByIndex = new Dictionary<int, QuestObjectiveProgressEntity>();

        foreach (var objectiveProgress in progress.Objectives)
        {
            if (!string.IsNullOrWhiteSpace(objectiveProgress.ObjectiveId))
            {
                var objectiveId = objectiveProgress.ObjectiveId.Trim();

                if (!existingByObjectiveId.TryGetValue(objectiveId, out var entries))
                {
                    entries = new Queue<QuestObjectiveProgressEntity>();
                    existingByObjectiveId[objectiveId] = entries;
                }

                entries.Enqueue(objectiveProgress);

                continue;
            }

            if (!legacyByIndex.ContainsKey(objectiveProgress.ObjectiveIndex))
            {
                legacyByIndex[objectiveProgress.ObjectiveIndex] = objectiveProgress;
            }
        }

        var changed = progress.Objectives.Count != quest.Objectives.Count;

        for (var index = 0; index < quest.Objectives.Count; index++)
        {
            var objective = quest.Objectives[index];
            var objectiveId = objective.ObjectiveId;
            QuestObjectiveProgressEntity? objectiveProgress = null;

            if (existingByObjectiveId.TryGetValue(objectiveId, out var entries) && entries.Count > 0)
            {
                objectiveProgress = entries.Dequeue();
            }
            else if (legacyByIndex.TryGetValue(index, out var legacyProgress))
            {
                objectiveProgress = legacyProgress;
            }

            if (objectiveProgress is null)
            {
                objectiveProgress = new QuestObjectiveProgressEntity
                {
                    CurrentAmount = 0,
                    IsCompleted = false
                };
                changed = true;
            }

            if (!string.Equals(objectiveProgress.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase))
            {
                objectiveProgress.ObjectiveId = objectiveId;
                changed = true;
            }

            if (objectiveProgress.ObjectiveIndex != index)
            {
                objectiveProgress.ObjectiveIndex = index;
                changed = true;
            }

            if (index >= progress.Objectives.Count || !ReferenceEquals(progress.Objectives[index], objectiveProgress))
            {
                changed = true;
            }

            normalized.Add(objectiveProgress);
        }

        if (!changed)
        {
            return false;
        }

        progress.Objectives = normalized;

        return true;
    }

    private bool TryGetQuest(string questId, out QuestTemplateDefinition quest)
    {
        var found = _questTemplateService.TryGet(questId, out var definition);
        quest = definition!;

        return found && definition is not null;
    }

    private static bool TryGetItemTemplateId(UOItemEntity item, out string? templateId)
        => item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out templateId) &&
           !string.IsNullOrWhiteSpace(templateId);

    private static void UpdateQuestStatus(QuestProgressEntity progress)
    {
        if (progress.Status == QuestProgressStatusType.Completed)
        {
            return;
        }

        progress.Status = progress.Objectives.All(static objective => objective.IsCompleted)
                              ? QuestProgressStatusType.ReadyToTurnIn
                              : QuestProgressStatusType.Active;
    }

    private bool ValidateRewardTemplates(QuestTemplateDefinition quest)
    {
        foreach (var reward in quest.Rewards)
        {
            if (reward.Gold > 0 && !_itemFactoryService.TryGetItemTemplate("gold", out _))
            {
                return false;
            }

            foreach (var rewardItem in reward.Items)
            {
                if (!_itemFactoryService.TryGetItemTemplate(rewardItem.ItemTemplateId, out _))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
