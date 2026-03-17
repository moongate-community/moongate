lilly = {}

local IDLE_TICK_MS = 1000

local function ensure_ai_ready(npc)
    if npc == nil then
        return false
    end

    ai_dialogue.init(npc, "lilly.txt")
    return true
end

function lilly.on_spawn(npc_id, _ctx)
    local npc = mobile.get(npc_id)
    ensure_ai_ready(npc)
end

function lilly.brain_loop(npc_id)
    while true do
        local npc = mobile.get(npc_id)
        if ensure_ai_ready(npc) then
            ai_dialogue.idle(npc)
        end

        coroutine.yield(IDLE_TICK_MS)
    end
end

function lilly.on_speech(npc_id, speaker_id, text, _speech_type, _map_id, _x, _y, _z)
    if tonumber(npc_id) == nil or tonumber(speaker_id) == nil or tonumber(npc_id) == tonumber(speaker_id) then
        return
    end

    local npc = mobile.get(npc_id)
    local speaker = mobile.get(speaker_id)

    if not ensure_ai_ready(npc) or speaker == nil then
        return
    end

    local speaker_name = speaker.name
    if speaker_name == nil or speaker_name == "" then
        return
    end

    if ai_dialogue.listener(npc, speaker, text) then
        return
    end

    npc:say("hello to you, " .. speaker_name .. "!")
end
