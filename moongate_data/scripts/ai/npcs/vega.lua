local tick = require("common.tick")

vega = {}

local state = {
    cadence = tick.state({
        move = 1100,
        speech = 2200,
        sound = 3200,
    }),
}

local MESSAGES = {
    "Miaaow! Voglio uscire in terrazza!",
    "Meow... Mi nascondo nell armadio!",
}

local SOUNDS = {
    0x69,
    0x6A,
}

function vega.on_think(npc_id)
    while true do
        local npc = mobile.get(npc_id)

        if npc ~= nil then
            local now = time.now_ms()

            tick.run(state.cadence, "move", now, function()
                npc:move(random.direction())
            end)

            tick.run(state.cadence, "speech", now, function()
                local message = random.element(MESSAGES)
                if message ~= nil then
                    npc:say(message)
                end
            end)

            tick.run(state.cadence, "sound", now, function()
                local sound_id = random.element(SOUNDS)
                if sound_id ~= nil then
                    npc:play_sound(sound_id)
                end
            end)
        end

        coroutine.yield(250)
    end
end

function vega.on_event(event_type, from_serial, event_obj)
    -- No event reactions for now.
end

function vega.on_death(by_character, context)
    -- TODO: death reaction hook.
end
