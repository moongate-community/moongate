-- Example item script. An item template with `ScriptId: magic_torch` runs these hooks.
-- Every hook is optional: define only the ones you need.

local magic_torch = {
    id = "magic_torch",
}

function magic_torch.on_double_click(ctx)
    log.info("magic torch " .. ctx.item.serial .. " was double-clicked")
end

function magic_torch.on_equip(ctx)
    log.info("magic torch equipped on layer " .. ctx.layer)
end

return magic_torch
