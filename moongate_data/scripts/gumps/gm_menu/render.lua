local add_section = require("gumps.gm_menu.sections.add")
local travel_section = require("gumps.gm_menu.sections.travel")

local render = {}

function render.add_content(layout, session_id, character_id, current_state, reopen_callback)
  if current_state.active_tab == "travel" then
    return travel_section.add_content(layout, session_id, character_id, reopen_callback)
  end

  return add_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
end

return render
