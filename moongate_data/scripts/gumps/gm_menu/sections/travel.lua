local teleports_controller = require("gumps.teleports.controller")

local travel_section = {}

function travel_section.add_content(layout, session_id, character_id, reopen_callback)
  local embedded_layout = teleports_controller.build_layout(
    session_id,
    character_id,
    reopen_callback,
    {
      origin_x = 188,
      origin_y = 20,
      show_outer_frame = false,
      title = "Travel Browser"
    }
  )

  for _, entry in ipairs(embedded_layout.ui) do
    layout.ui[#layout.ui + 1] = entry
  end

  return embedded_layout.handlers.on_click
end

return travel_section
