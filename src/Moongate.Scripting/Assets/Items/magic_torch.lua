-- Example item script. An item template with `ScriptId: items.magic_torch` runs these hooks —
-- the dot is a namespace, so that id is this file, scripts/items/magic_torch.lua.
-- Every hook is optional: define only the ones you need.

local magic_torch = {
    id = "items.magic_torch",
}

function magic_torch.on_double_click(ctx)
    log.info("magic torch " .. ctx.item.serial .. " was double-clicked")
end

function magic_torch.on_equip(ctx)
    log.info("magic torch equipped on layer " .. ctx.layer)
end

return magic_torch
