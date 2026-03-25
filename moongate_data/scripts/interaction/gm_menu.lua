local gm_menu = require("gumps.gm_menu")

function on_gm_menu_request(session_id, character_id)
  if session_id == nil or character_id == nil then
    return false
  end

  return gm_menu.open(session_id, character_id)
end
