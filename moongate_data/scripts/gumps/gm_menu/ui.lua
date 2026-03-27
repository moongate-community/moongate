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
  local function get_tab_hue(tab_name)
    if current_state.active_tab == tab_name then
      return c.ACCENT_HUE
    end

    return c.LABEL_HUE
  end

  push(layout_ui, { type = "label", x = 32, y = 62, hue = c.TITLE_HUE, text = "Tools" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_ADD, x = 32, y = 94, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 96, hue = get_tab_hue("add"), text = "Add" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_TRAVEL, x = 32, y = 126, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 128, hue = get_tab_hue("travel"), text = "Travel" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_PROBE, x = 32, y = 158, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 160, hue = get_tab_hue("probe"), text = "Probe" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_SPAWN, x = 32, y = 190, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 192, hue = get_tab_hue("spawn"), text = "Spawn" })

  push(layout_ui, { type = "button", id = c.BUTTON_TAB_BROADCAST, x = 32, y = 222, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 64, y = 224, hue = get_tab_hue("broadcast"), text = "Broadcast" })
end

return ui
