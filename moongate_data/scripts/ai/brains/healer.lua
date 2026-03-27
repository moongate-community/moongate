-- Placeholder: healer brain stub. Implement healing logic (resurrect, cure, heal nearby players).
healer = {}

local TICK_DELAY_MS = 3000

function healer.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            coroutine.yield(TICK_DELAY_MS)
        end
    end
end

function healer.on_death(_by_character, _context)
end
