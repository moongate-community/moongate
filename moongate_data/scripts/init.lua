require("ai.orion")
require("items.apple")
require("items.brick")
require("items.door")
local teleports = require("gumps.teleports")

function on_player_connected(p)
    log.info("Anvedi che s'e connesson un client")
end

function on_ready()
    log.info("Random direction is " .. random.direction())
end

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
