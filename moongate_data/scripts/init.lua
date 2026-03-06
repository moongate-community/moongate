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

command.register("eclipse", function(ctx)
    weather.set_global_light(26)
    speech.broadcast("The moon has blocked the sun.")

    if ctx.session_id ~= nil and ctx.session_id > 0 then
        ctx:print("Eclipse started.")
    end
end, {
    description = "Starts a world eclipse and broadcasts a global message.",
    minimum_account_type = "Administrator",
})
