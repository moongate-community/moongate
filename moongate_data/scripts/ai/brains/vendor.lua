-- Placeholder: vendor brain stub. Implement trade/shop interaction logic.
vendor = {}

local TICK_DELAY_MS = 5000

function vendor.on_think(npc_serial)
    while true do
        local npc = mobile.get(npc_serial)

        if npc == nil then
            coroutine.yield(TICK_DELAY_MS)
        else
            coroutine.yield(TICK_DELAY_MS)
        end
    end
end

function vendor.on_death(_by_character, _context)
end
