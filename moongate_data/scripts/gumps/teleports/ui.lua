local c = require("gumps.teleports.constants")

local ui = {}

local function push(layout_ui, entry)
  layout_ui[#layout_ui + 1] = entry
end

ui.push = push

function ui.create_view(options)
  local resolved = options or {}

  return {
    origin_x = tonumber(resolved.origin_x) or 0,
    origin_y = tonumber(resolved.origin_y) or 0,
    show_outer_frame = resolved.show_outer_frame ~= false,
    show_refresh = resolved.show_refresh ~= false,
    title = tostring(resolved.title or "Teleport Browser")
  }
end

function ui.add_frame(layout_ui, view)
  local ox = view.origin_x or 0
  local oy = view.origin_y or 0

  if view.show_outer_frame then
    push(layout_ui, { type = "background", x = ox, y = oy, gump_id = 5054, width = c.GUMP_WIDTH, height = c.GUMP_HEIGHT })
    push(layout_ui, { type = "alpha_region", x = ox + 10, y = oy + 10, width = 500, height = 400 })
  end

  push(layout_ui, { type = "image_tiled", x = ox + 10, y = oy + 10, width = 500, height = 22, gump_id = 2624 })
  push(layout_ui, { type = "alpha_region", x = ox + 10, y = oy + 10, width = 500, height = 22 })
  push(layout_ui, { type = "label", x = ox + 20, y = oy + 14, hue = c.TITLE_HUE, text = view.title })

  if view.show_refresh then
    push(layout_ui, { type = "button", id = c.BUTTON_REFRESH, x = ox + 420, y = oy + 12, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    push(layout_ui, { type = "label", x = ox + 450, y = oy + 14, hue = c.LABEL_HUE, text = "Refresh" })
  end

  push(layout_ui, { type = "image_tiled", x = ox + 10, y = oy + 40, width = 500, height = 300, gump_id = 2624 })
  push(layout_ui, { type = "alpha_region", x = ox + 10, y = oy + 40, width = 500, height = 300 })

  push(layout_ui, { type = "image_tiled", x = ox + 10, y = oy + 350, width = 500, height = 60, gump_id = 2624 })
  push(layout_ui, { type = "alpha_region", x = ox + 10, y = oy + 350, width = 500, height = 60 })
end

function ui.add_page_nav(layout_ui, page, pages, view)
  local ox = view.origin_x or 0
  local oy = view.origin_y or 0

  push(layout_ui, { type = "button", id = c.BUTTON_PREV_PAGE, x = ox + 20, y = oy + 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
  push(layout_ui, { type = "button", id = c.BUTTON_NEXT_PAGE, x = ox + 60, y = oy + 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(layout_ui, { type = "label", x = ox + 95, y = oy + 364, hue = c.LABEL_HUE, text = "Page " .. tostring(page) .. "/" .. tostring(pages) })
end

return ui
