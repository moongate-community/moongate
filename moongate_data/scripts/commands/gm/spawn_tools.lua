local spawn_tools = require("gumps.spawn_tools")

command.register("spawn_tools", function(ctx)
    if ctx.session_id == nil or ctx.session_id <= 0 then
        ctx:print_error("This command can only be used in-game.")
        return
    end

    local ok = spawn_tools.open(ctx.session_id, ctx.character_id or 0)
    if not ok then
        ctx:print_error("Failed to open spawn tools gump.")
    end
end, {
    description = "Open spawn tools gump."
})
