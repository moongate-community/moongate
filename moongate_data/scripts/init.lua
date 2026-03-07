require("ai.orion")
require("commands.gm.eclipse")
require("commands.gm.set_world_light")
require("commands.gm.teleports")
require("items.apple")
require("items.brick")
require("items.door")
require("items.teleport")

function on_player_connected(p)
    log.info("Anvedi che s'e connesso un client")
end

function on_ready()
    log.info("Random direction is " .. random.direction())
end
