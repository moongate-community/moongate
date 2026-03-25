local add_section = require("gumps.gm_menu.sections.add")
local travel_section = require("gumps.gm_menu.sections.travel")

local render = {}

function render.add_content(layout_ui, current_state)
  if current_state.active_tab == "travel" then
    travel_section.add_content(layout_ui)
    return
  end

  add_section.add_content(layout_ui)
end

return render
