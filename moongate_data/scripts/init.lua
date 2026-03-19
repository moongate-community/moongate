require("ai.init")
require("commands.gm.init")
require("interaction.init")
require("items.init")

function on_player_connected(p)
    log.info("Anvedi che s'e connesso un client")
end

function on_ready()
    log.info("Random direction is " .. random.direction())
end
