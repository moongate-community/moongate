local utility_runner = require("ai.runners.utility_runner")

utility_npc = {}

local BEHAVIORS = {
    "evade",
    "follow",
    "idle",
}

function utility_npc.on_think(npc_serial)
    while true do
        local ctx = {
            now_ms = time.now_ms(),
            min_hold_ms = 500,
        }

        local delay_ms = utility_runner.tick(npc_serial, ctx, BEHAVIORS)
        coroutine.yield(delay_ms)
    end
end

function utility_npc.on_event(event_type, from_serial, event_obj)
    local npc_serial = 0

    if type(event_obj) == "table" then
        npc_serial = tonumber(event_obj.listener_npc_id or event_obj.mobile_id or 0) or 0
    end

    if npc_serial <= 0 then
        return
    end

    utility_runner.on_event(npc_serial, {}, event_type, from_serial, event_obj)
end

function utility_npc.on_death(_by_character, _context)
    -- TODO: add death reaction in concrete brains if needed.
end
