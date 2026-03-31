local quest_dialog = require("gumps.quests.quest_dialog")

function on_quest_dialog_requested(session_id, character_id, npc_serial)
    if session_id == nil or character_id == nil or npc_serial == nil then
        return false
    end

    return quest_dialog.open(session_id, character_id, npc_serial)
end
