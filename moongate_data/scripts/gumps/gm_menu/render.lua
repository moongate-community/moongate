local add_section = require("gumps.gm_menu.sections.add")
local broadcast_section = require("gumps.gm_menu.sections.broadcast")
local probe_section = require("gumps.gm_menu.sections.probe")
local spawn_section = require("gumps.gm_menu.sections.spawn")
local travel_section = require("gumps.gm_menu.sections.travel")

local render = {}

function render.add_content(layout, session_id, character_id, current_state, reopen_callback)
  if current_state.active_tab == "travel" then
    return travel_section.add_content(layout, session_id, character_id, reopen_callback)
  end

  if current_state.active_tab == "probe" then
    return probe_section.add_content(layout, session_id, character_id, reopen_callback)
  end

  if current_state.active_tab == "spawn" then
    return spawn_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
  end

  if current_state.active_tab == "broadcast" then
    return broadcast_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
  end

  return add_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
end

return render
