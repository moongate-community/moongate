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

[ScriptModule("quests", "Provides quest dialog helpers for Lua scripts.")]
public sealed class QuestsModule
{
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
        if (sessionId <= 0 || characterId == 0 || npcSerial == 0)
        {
            return false;
        }

        _scriptEngineService.CallFunction("on_quest_dialog_requested", sessionId, characterId, npcSerial);

        return true;
    }

    [ScriptFunction("get_available", "Returns quests available from the specified NPC.")]
    public Table GetAvailable(long sessionId, uint characterId, uint npcSerial)
        => BuildQuestTable(sessionId, characterId, npcSerial, includeActiveProgress: false);

    [ScriptFunction("get_active", "Returns active quests relevant to the specified NPC.")]
    public Table GetActive(long sessionId, uint characterId, uint npcSerial)
        => BuildQuestTable(sessionId, characterId, npcSerial, includeActiveProgress: true);

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

    private bool TryResolvePlayerAndNpc(
        long sessionId,
        uint characterId,
        uint npcSerial,
        out UOMobileEntity player,
        out UOMobileEntity npc
    )
    {
        player = null!;
        npc = null!;

        if (sessionId <= 0 || characterId == 0 || npcSerial == 0)
        {
            return false;
        }

        if (!_gameNetworkSessionService.TryGet(sessionId, out var session))
        {
            return false;
        }

        if (session.Character is null ||
            !session.Character.IsPlayer ||
            session.Character.Id != (Serial)characterId)
        {
            return false;
        }

        var resolvedNpc = _mobileService.GetAsync((Serial)npcSerial).GetAwaiter().GetResult();

        if (resolvedNpc is null)
        {
            return false;
        }

        player = session.Character;
        npc = resolvedNpc;

        return true;
    }
}
