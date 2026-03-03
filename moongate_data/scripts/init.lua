require("ai.orion")

function on_player_connected(p)
    log.info("Anvedi che s'e connesson un client")
end

function on_ready()
    log.info("Random direction is " .. random.direction())
end
