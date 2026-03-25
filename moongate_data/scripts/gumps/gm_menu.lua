local c = require("gumps.gm_menu.constants")
local controller = require("gumps.gm_menu.controller")

local gm_menu = {}

function gm_menu.open(session_id, character_id)
  local layout, sender_serial = controller.build_layout(
    session_id,
    character_id,
    function(target_session, target_character)
      gm_menu.open(target_session, target_character)
    end
  )

  return gump.send_layout(session_id, layout, sender_serial, c.GUMP_ID, c.GUMP_X, c.GUMP_Y)
end

return gm_menu
