local c = require("gumps.teleports.constants")

local ui = {}

local function push(layout_ui, entry)
  layout_ui[#layout_ui + 1] = entry
end

ui.push = push

function ui.add_frame(layout_ui)
  push(layout_ui, { type = "background", x = 0, y = 0, gump_id = 5054, width = c.GUMP_WIDTH, height = c.GUMP_HEIGHT })
  push(layout_ui, { type = "alpha_region", x = 10, y = 10, width = 500, height = 400 })

  push(layout_ui, { type = "image_tiled", x = 10, y = 10, width = 500, height = 22, gump_id = 2624 })
  push(layout_ui, { type = "alpha_region", x = 10, y = 10, width = 500, height = 22 })
  push(layout_ui, { type = "label", x = 20, y = 14, hue = c.TITLE_HUE, text = "Teleport Browser" })

  push(layout_ui, { type = "button", id = c.BUTTON_REFRESH, x = 420, y = 12, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 450, y = 14, hue = c.LABEL_HUE, text = "Refresh" })

  push(layout_ui, { type = "image_tiled", x = 10, y = 40, width = 500, height = 300, gump_id = 2624 })
  push(layout_ui, { type = "alpha_region", x = 10, y = 40, width = 500, height = 300 })

  push(layout_ui, { type = "image_tiled", x = 10, y = 350, width = 500, height = 60, gump_id = 2624 })
  push(layout_ui, { type = "alpha_region", x = 10, y = 350, width = 500, height = 60 })
end

function ui.add_page_nav(layout_ui, page, pages)
  push(layout_ui, { type = "button", id = c.BUTTON_PREV_PAGE, x = 20, y = 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
  push(layout_ui, { type = "button", id = c.BUTTON_NEXT_PAGE, x = 60, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = 95, y = 364, hue = c.LABEL_HUE, text = "Page " .. tostring(page) .. "/" .. tostring(pages) })
end

return ui
