command.register("set_world_light", function(ctx)
    local args = ctx.arguments or {}
    if #args < 1 then
        ctx:print_error("Usage: set_world_light <0-255>")
        return
    end

    local level = tonumber(args[1])
    if level == nil then
        ctx:print_error("Invalid value. Expected number in range 0-255.")
        return
    end

    level = math.floor(level)
    if level < 0 or level > 255 then
        ctx:print_error("Invalid value. Expected number in range 0-255.")
        return
    end

    weather.set_global_light(level)
    ctx:print("World light set to " .. tostring(level) .. ".")
end, {
    description = "Sets the global world light level. Usage: set_world_light <0-255>",
    minimum_account_type = "Administrator",
})
