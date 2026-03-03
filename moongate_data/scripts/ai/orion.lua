orion = {}

local MOVE_INTERVAL_MS = 1000
local SPEECH_INTERVAL_MS = 2000

local state = {
    last_move_ms = 0,
    last_speech_ms = 0,
}

local MESSAGES = {
    "Meow meow! I'm hungry!",
    "Meow, I want kibble!",
}

function orion.brain_loop(npc_id)
    while true do
        local npc = mobile.get(npc_id)

        if npc ~= nil then
            local now = time.now_ms()

            if now - state.last_move_ms >= MOVE_INTERVAL_MS then
                npc:Move(random.direction())
                log.info("Orion moves in a random direction.")
                state.last_move_ms = now
            end

            if now - state.last_speech_ms >= SPEECH_INTERVAL_MS then
                local index = random.int(1, #MESSAGES)
                log.info("Orion says: %s", MESSAGES[index])
                npc:Say(MESSAGES[index])
                state.last_speech_ms = now
            end
        end

        coroutine.yield(250)
    end
end

function orion.on_event(event_type, from_serial, event_obj)
    -- No event reactions for now.
end
