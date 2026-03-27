local c = require("gumps.gm_menu.constants")

local ui = {}

local function push(layout_ui, entry)
  layout_ui[#layout_ui + 1] = entry
end

ui.push = push

function ui.add_frame(layout_ui)
  push(layout_ui, { type = "background", x = 0, y = 0, gump_id = 5054, width = c.GUMP_WIDTH, height = c.GUMP_HEIGHT })
  push(layout_ui, { type = "image_tiled", x = 12, y = 12, width = c.GUMP_WIDTH - 24, height = 24, gump_id = 2624 })
  push(layout_ui, { type = "label", x = 24, y = 16, hue = c.TITLE_HUE, text = "GM Menu" })

  push(layout_ui, { type = "image_tiled", x = 20, y = 48, width = c.SIDEBAR_WIDTH, height = c.GUMP_HEIGHT - 68, gump_id = 2624 })
end

function ui.add_sidebar(layout_ui, current_state)
  local add_hue = c.LABEL_HUE
  local travel_hue = c.LABEL_HUE
  local probe_hue = c.LABEL_HUE

  if current_state.active_tab == "add" then
    add_hue = c.ACCENT_HUE
  elseif current_state.active_tab == "travel" then
    travel_hue = c.ACCENT_HUE
  else
    probe_hue = c.ACCENT_HUE
  end

  push(layout_ui, { type = "label", x = 32, y = 62, hue = c.TITLE_HUE, text = "Tools" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_ADD, x = 32, y = 94, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 96, hue = add_hue, text = "Add" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_TRAVEL, x = 32, y = 126, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 128, hue = travel_hue, text = "Travel" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_PROBE, x = 32, y = 158, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 160, hue = probe_hue, text = "Probe" })
end

return ui
