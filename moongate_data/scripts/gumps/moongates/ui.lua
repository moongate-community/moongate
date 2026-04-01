local header = require("gumps.layout.header")

local ui = {}
local LEFT_PANE_X = 18
local LEFT_PANE_WIDTH = 130
local RIGHT_PANE_X = 160
local RIGHT_PANE_WIDTH = 282
local PANE_BOTTOM_MARGIN = 18
local PANE_HEADER_GAP = 8
local PANE_CONTENT_GAP = 28

local function push(layout_ui, entry)
  layout_ui[#layout_ui + 1] = entry
end

function ui.push(layout_ui, entry)
  push(layout_ui, entry)
end

function ui.add_frame(layout_ui, constants)
  push(layout_ui, { type = "background", x = 0, y = 0, gump_id = 9200, width = constants.WIDTH, height = constants.HEIGHT })
  push(layout_ui, { type = "alpha_region", x = 12, y = 12, width = constants.WIDTH - 24, height = constants.HEIGHT - 24 })

  local content_y = header.add(layout_ui, {
    x = 24,
    y = 24,
    width = constants.WIDTH - 48,
    title = "Public Moongate",
    subtitle = "Choose a destination from the shard-wide moongate network."
  })
  local pane_y = content_y + 2
  local pane_height = constants.HEIGHT - pane_y - PANE_BOTTOM_MARGIN

  push(layout_ui, { type = "background", x = LEFT_PANE_X, y = pane_y, gump_id = 9200, width = LEFT_PANE_WIDTH, height = pane_height })
  push(layout_ui, { type = "background", x = RIGHT_PANE_X, y = pane_y, gump_id = 9200, width = RIGHT_PANE_WIDTH, height = pane_height })

  push(layout_ui, { type = "button", id = constants.BUTTON_CLOSE, x = constants.WIDTH - 42, y = 18, normal_id = 4017, pressed_id = 4019, onclick = "on_click" })
  push(layout_ui, { type = "label", x = LEFT_PANE_X + 12, y = pane_y + PANE_HEADER_GAP, hue = 1152, text = "Realms" })
  push(layout_ui, { type = "label", x = RIGHT_PANE_X + 12, y = pane_y + PANE_HEADER_GAP, hue = 1152, text = "Destinations" })

  return pane_y + PANE_CONTENT_GAP
end

function ui.add_group_button(layout_ui, constants, x, y, button_id, text, selected)
  local hue = selected and 1153 or 1102

  push(layout_ui, { type = "button", id = button_id, x = x, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = x + 32, y = y + 2, hue = hue, text = tostring(text or "") })
end

function ui.add_destination_button(layout_ui, constants, x, y, button_id, text)
  push(layout_ui, { type = "button", id = button_id, x = x, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = x + 32, y = y + 2, hue = 1152, text = tostring(text or "") })
end

return ui
