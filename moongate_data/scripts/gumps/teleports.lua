local c = require("gumps.teleports.constants")
local controller = require("gumps.teleports.controller")

local teleports = {}

function teleports.open(session_id, character_id)
  local layout, sender_serial = controller.build_layout(
    session_id,
    character_id,
    function(target_session, target_character)
      teleports.open(target_session, target_character)
    end
  )

  return gump.send_layout(session_id, layout, sender_serial, c.GUMP_ID, c.GUMP_X, c.GUMP_Y)
end

return teleports
