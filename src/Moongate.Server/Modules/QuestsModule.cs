using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("quests", "Provides quest dialog and journal helpers for Lua scripts.")]
public sealed class QuestsModule
{
    private const int InteractionRange = 18;

    private readonly IQuestService _questService;
    private readonly IQuestTemplateService _questTemplateService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IMobileService _mobileService;
    private readonly IScriptEngineService _scriptEngineService;

    public QuestsModule(
        IQuestService questService,
        IQuestTemplateService questTemplateService,
        IGameNetworkSessionService gameNetworkSessionService,
        IMobileService mobileService,
        IScriptEngineService scriptEngineService
    )
    {
        _questService = questService;
        _questTemplateService = questTemplateService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _mobileService = mobileService;
        _scriptEngineService = scriptEngineService;
    }

    [ScriptFunction("open", "Opens the shared quest dialog for a session.")]
    public bool Open(long sessionId, uint characterId, uint npcSerial)
    {
        if (!TryResolvePlayerAndNpc(sessionId, characterId, npcSerial, out _, out _))
        {
            return false;
        }

        var result = _scriptEngineService.ExecuteFunction(
            $"on_quest_dialog_requested({sessionId}, {characterId}, {npcSerial})"
        );

        return result.Success && result.Data is bool opened && opened;
    }

    [ScriptFunction("open_journal", "Validates that the shared quest journal can be opened for the specified session.")]
    public bool OpenJournal(long sessionId, uint characterId)
        => TryResolvePlayer(sessionId, characterId, out _);

    [ScriptFunction("get_available", "Returns quests available from the specified NPC.")]
    public Table GetAvailable(long sessionId, uint characterId, uint npcSerial)
        => BuildQuestTable(sessionId, characterId, npcSerial, includeActiveProgress: false);

    [ScriptFunction("get_active", "Returns active quests relevant to the specified NPC.")]
    public Table GetActive(long sessionId, uint characterId, uint npcSerial)
        => BuildQuestTable(sessionId, characterId, npcSerial, includeActiveProgress: true);

    [ScriptFunction("get_journal", "Returns the player's quest journal with objective progress.")]
    public Table GetJournal(long sessionId, uint characterId)
        => BuildJournalTable(sessionId, characterId);

    [ScriptFunction("accept", "Accepts a quest from the specified NPC.")]
    public bool Accept(long sessionId, uint characterId, uint npcSerial, string questId)
    {
        if (!TryResolvePlayerAndNpc(sessionId, characterId, npcSerial, out var player, out var npc) ||
            string.IsNullOrWhiteSpace(questId))
        {
            return false;
        }

        return _questService.AcceptAsync(player, npc, questId.Trim()).GetAwaiter().GetResult();
    }

    [ScriptFunction("complete", "Completes a ready quest at the specified NPC.")]
    public bool Complete(long sessionId, uint characterId, uint npcSerial, string questId)
    {
        if (!TryResolvePlayerAndNpc(sessionId, characterId, npcSerial, out var player, out var npc) ||
            string.IsNullOrWhiteSpace(questId))
        {
            return false;
        }

        return _questService.TryCompleteAsync(player, npc, questId.Trim()).GetAwaiter().GetResult();
    }

    private Table BuildQuestTable(
        long sessionId,
        uint characterId,
        uint npcSerial,
        bool includeActiveProgress
    )
    {
        var table = new Table(null);

        if (!TryResolvePlayerAndNpc(sessionId, characterId, npcSerial, out var player, out var npc))
        {
            return table;
        }

        var index = 1;

        if (!includeActiveProgress)
        {
            var available = _questService.GetAvailableForNpcAsync(player, npc).GetAwaiter().GetResult();

            foreach (var quest in available)
            {
                table[index++] = CreateAvailableQuestEntry(quest);
            }

            return table;
        }

        var active = _questService.GetActiveForNpcAsync(player, npc).GetAwaiter().GetResult();

        foreach (var progress in active)
        {
            if (!_questTemplateService.TryGet(progress.QuestId, out var template) || template is null)
            {
                continue;
            }

            table[index++] = CreateActiveQuestEntry(template, progress);
        }

        return table;
    }

    private Table BuildJournalTable(long sessionId, uint characterId)
    {
        var table = new Table(null);

        if (!TryResolvePlayer(sessionId, characterId, out var player))
        {
            return table;
        }

        var journal = _questService.GetJournalAsync(player).GetAwaiter().GetResult();
        var index = 1;

        foreach (var progress in journal)
        {
            if (!_questTemplateService.TryGet(progress.QuestId, out var template) || template is null)
            {
                continue;
            }

            table[index++] = CreateJournalQuestEntry(template, progress);
        }

        return table;
    }

    private static Table CreateAvailableQuestEntry(QuestTemplateDefinition quest)
    {
        var entry = new Table(null)
        {
            ["quest_id"] = quest.Id,
            ["name"] = quest.Name,
            ["description"] = quest.Description,
            ["category"] = quest.Category,
            ["is_ready_to_turn_in"] = false,
            ["status_text"] = "Available"
        };

        return entry;
    }

    private static Table CreateActiveQuestEntry(QuestTemplateDefinition quest, QuestProgressEntity progress)
    {
        var statusText = progress.Status == QuestProgressStatusType.ReadyToTurnIn
                             ? "Ready to turn in"
                             : "In progress";

        var entry = new Table(null)
        {
            ["quest_id"] = quest.Id,
            ["name"] = quest.Name,
            ["description"] = quest.Description,
            ["category"] = quest.Category,
            ["is_ready_to_turn_in"] = progress.Status == QuestProgressStatusType.ReadyToTurnIn,
            ["status_text"] = statusText
        };

        return entry;
    }

    private static Table CreateJournalQuestEntry(QuestTemplateDefinition quest, QuestProgressEntity progress)
    {
        var entry = CreateActiveQuestEntry(quest, progress);
        entry["objectives"] = CreateJournalObjectivesTable(quest, progress);

        return entry;
    }

    private static Table CreateJournalObjectivesTable(QuestTemplateDefinition quest, QuestProgressEntity progress)
    {
        var objectives = new Table(null);

        for (var index = 0; index < quest.Objectives.Count; index++)
        {
            var objective = quest.Objectives[index];
            var objectiveProgress = index < progress.Objectives.Count ? progress.Objectives[index] : null;
            objectives[index + 1] = CreateJournalObjectiveEntry(objective, objectiveProgress);
        }

        return objectives;
    }

    private static Table CreateJournalObjectiveEntry(
        QuestObjectiveDefinition objective,
        QuestObjectiveProgressEntity? objectiveProgress
    )
    {
        var currentAmount = objectiveProgress?.CurrentAmount ?? 0;
        var amount = Math.Max(0, objective.Amount);
        var isCompleted = objectiveProgress?.IsCompleted == true || (amount > 0 && currentAmount >= amount);
        var progressText = $"{currentAmount} / {amount}";

        var entry = new Table(null)
        {
            ["objective_index"] = objectiveProgress?.ObjectiveIndex ?? 0,
            ["objective_type"] = objective.Type.ToString(),
            ["objective_text"] = DescribeObjective(objective),
            ["current_amount"] = currentAmount,
            ["amount"] = amount,
            ["progress_text"] = progressText,
            ["is_completed"] = isCompleted,
            ["status_text"] = isCompleted ? "Complete" : "In progress"
        };

        return entry;
    }

    private bool TryResolvePlayerAndNpc(
        long sessionId,
        uint characterId,
        uint npcSerial,
        out UOMobileEntity player,
        out UOMobileEntity npc
    )
    {
        if (!TryResolvePlayer(sessionId, characterId, out player))
        {
            npc = null!;

            return false;
        }

        if (npcSerial == 0)
        {
            npc = null!;

            return false;
        }

        var resolvedNpc = _mobileService.GetAsync((Serial)npcSerial).GetAwaiter().GetResult();

        if (resolvedNpc is null)
        {
            npc = null!;

            return false;
        }

        if (resolvedNpc.IsPlayer)
        {
            npc = null!;

            return false;
        }

        if (_gameNetworkSessionService.TryGet(sessionId, out var session) &&
            session.AccountType < AccountType.GameMaster &&
            (player.MapId != resolvedNpc.MapId || !player.Location.InRange(resolvedNpc.Location, InteractionRange)))
        {
            npc = null!;

            return false;
        }

        npc = resolvedNpc;

        return true;
    }

    private bool TryResolvePlayer(long sessionId, uint characterId, out UOMobileEntity player)
    {
        player = null!;

        if (sessionId <= 0 || characterId == 0)
        {
            return false;
        }

        if (!_gameNetworkSessionService.TryGet(sessionId, out var session))
        {
            return false;
        }

        if (session.Character is null || !session.Character.IsPlayer || session.Character.Id != (Serial)characterId)
        {
            return false;
        }

        player = session.Character;

        return true;
    }

    private static string DescribeObjective(QuestObjectiveDefinition objective)
    {
        var target = objective.Type switch
        {
            QuestObjectiveType.Kill =>
                objective.MobileTemplateIds.Count == 0 ? string.Empty : string.Join(", ", objective.MobileTemplateIds),
            QuestObjectiveType.Collect or QuestObjectiveType.Deliver => objective.ItemTemplateId ?? string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(target))
        {
            return objective.Type.ToString();
        }

        return $"{objective.Type} {target}";
    }
}
