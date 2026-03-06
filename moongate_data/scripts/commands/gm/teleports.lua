local teleports = require("gumps.teleports")

command.register("teleports", function(ctx)
    if ctx.session_id == nil or ctx.session_id <= 0 then
        ctx:print_error("This command can only be used in-game.")
        return
    end

    local ok = teleports.open(ctx.session_id, 0)
    if not ok then
        ctx:print_error("Failed to open teleports gump.")
    end
end, {
    description = "Open teleport browser gump.",
})
