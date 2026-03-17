local tick = require("common.tick")

orion = {}

local state = {
    cadence = tick.state({
        move = 5000,
        speech = 2000,
        sound = 3000,
    }),
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

            tick.run(state.cadence, "move", now, function()
                npc:move(random.direction())
                log.info("Orion moves in a random direction.")
            end)

            tick.run(state.cadence, "speech", now, function()
                local message = random.element(MESSAGES)
                if message ~= nil then
                    log.info("Orion says: {0}", message)
                    npc:say(message)
                end
            end)

            tick.run(state.cadence, "sound", now, function()
                local sound_id = random.element(SOUNDS)
                if sound_id ~= nil then
                    npc:play_sound(sound_id)
                    log.info("Orion plays sound with ID: {0}", sound_id)
                end
            end)
        end

        coroutine.yield(250)
    end
end

function orion.on_event(event_type, from_serial, event_obj)
    -- No event reactions for now.
end

function orion.get_context_menus(ctx)
    return {
        { key = "give_food", cliloc_id = 3006135 },
    }
end

function orion.on_selected_context_menu(menu_key, ctx)
    if menu_key ~= "give_food" then
        return
    end

    local npc = mobile.get(ctx.target_mobile_id)
    if npc == nil then
        return
    end

    npc:say("meeow")
end

function orion.on_death(by_character, context)
    -- TODO: death reaction hook.
end
