orion = {}

local MOVE_INTERVAL_MS = 1000
local SPEECH_INTERVAL_MS = 2000
local SOUND_INTERVAL_MS = 3000

local state = {
    last_move_ms = 0,
    last_speech_ms = 0,
    last_sound_ms = 0,
}

local MESSAGES = {
    "Meow meow! Ho fame!",
    "Meow, Voglio i chicchini!",
}

local SOUNDS = {
    0x69,
    0x6A,
}

function orion.brain_loop(npc_id)
    while true do
        local npc = mobile.get(npc_id)

        if npc ~= nil then
            local now = time.now_ms()

            if now - state.last_move_ms >= MOVE_INTERVAL_MS then
                npc:move(random.direction())
                log.info("Orion moves in a random direction.")
                state.last_move_ms = now
            end

            if now - state.last_speech_ms >= SPEECH_INTERVAL_MS then
                local message = random.element(MESSAGES)
                if message ~= nil then
                    log.info("Orion says: {0}", message)
                    npc:say(message)
                end
                state.last_speech_ms = now
            end

            if now - state.last_sound_ms >= SOUND_INTERVAL_MS then
                local sound_id = random.element(SOUNDS)
                if sound_id ~= nil then
                    npc:play_sound(sound_id)
                    log.info("Orion plays sound with ID: {0}", sound_id)
                end
                state.last_sound_ms = now
            end
        end

        coroutine.yield(250)
    end
end

function orion.on_event(event_type, from_serial, event_obj)
    -- No event reactions for now.
end

function orion.on_death(by_character, context)
    -- TODO: death reaction hook.
end
