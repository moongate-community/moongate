local header = require("gumps.layout.header")

local ui = {}

local function push(layout_ui, entry)
  layout_ui[#layout_ui + 1] = entry
end

function ui.push(layout_ui, entry)
  push(layout_ui, entry)
end

function ui.add_frame(layout_ui, constants)
  push(layout_ui, { type = "background", x = 0, y = 0, gump_id = 5054, width = constants.WIDTH, height = constants.HEIGHT })
  push(layout_ui, { type = "alpha_region", x = 10, y = 10, width = constants.WIDTH - 20, height = constants.HEIGHT - 20 })

  local content_y = header.add(layout_ui, {
    x = 24,
    y = 20,
    width = constants.WIDTH - 48,
    title = "Public Moongate",
    subtitle = "Choose a destination from the shard-wide moongate network."
  })

  push(layout_ui, { type = "button", id = constants.BUTTON_CLOSE, x = constants.WIDTH - 42, y = 18, normal_id = 4017, pressed_id = 4019, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 24, y = content_y + 4, hue = 1152, text = "Realms" })
  push(layout_ui, { type = "label", x = 180, y = content_y + 4, hue = 1152, text = "Destinations" })

  return content_y + 28
end

function ui.add_group_button(layout_ui, constants, x, y, button_id, text, selected)
  local hue = selected and 1153 or 1102

  push(layout_ui, { type = "button", id = button_id, x = x, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = x + 32, y = y + 2, hue = hue, text = tostring(text or "") })
end

function ui.add_destination_button(layout_ui, constants, x, y, button_id, text)
  push(layout_ui, { type = "button", id = button_id, x = x, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = x + 32, y = y + 2, hue = 0, text = tostring(text or "") })
end

return ui
