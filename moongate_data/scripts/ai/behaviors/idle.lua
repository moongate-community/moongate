local behavior = require("ai.behavior")

local M = {}

function M.score(_npc_serial, _ctx)
    return 1
end

function M.run(npc_serial, _ctx)
    steering.wander(npc_serial, 4)
    return 500
end

behavior.register("idle", M)

return M
