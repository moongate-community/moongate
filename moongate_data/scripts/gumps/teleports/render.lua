local c = require("gumps.teleports.constants")
local d = require("gumps.teleports.data")
local ui = require("gumps.teleports.ui")

local render = {}

function render.ensure_valid_map(state, maps)
  if state.map_id == nil then
    state.map_id = maps[1].map_id
    return
  end

  local map_ok = false
  for _, m in ipairs(maps) do
    if m.map_id == state.map_id then
      map_ok = true
      break
    end
  end

  if not map_ok then
    state.map_id = maps[1].map_id
    state.category = nil
    state.selected = nil
    state.page = 1
  end
end

function render.map_step(layout_ui, state, maps)
  local total_pages
  state.page, total_pages = d.clamp_page(state.page, #maps, c.MAP_ROWS)

  local start_idx = (state.page - 1) * c.MAP_ROWS + 1
  local end_idx = math.min(#maps, start_idx + c.MAP_ROWS - 1)
  state.visible_maps = {}

  ui.push(layout_ui, { type = "label", x = 24, y = 48, hue = c.TITLE_HUE, text = "Step 1/3 - Select map" })

  local row = 1
  local y = 72
  for i = start_idx, end_idx do
    local map_entry = maps[i]
    state.visible_maps[row] = map_entry
    local prefix = (map_entry.map_id == state.map_id) and "* " or "  "

    ui.push(layout_ui, { type = "button", id = c.BUTTON_MAP_BASE + row, x = 22, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 50,
      y = y + 2,
      width = 430,
      height = 20,
      hue = c.LIST_HUE,
      text = d.trunc(prefix .. map_entry.map_name .. " (" .. tostring(map_entry.map_id) .. ")", 58)
    })

    y = y + c.MAP_ROW_HEIGHT
    row = row + 1
  end

  ui.add_page_nav(layout_ui, state.page, total_pages)
  ui.push(layout_ui, { type = "button", id = c.BUTTON_TO_CATEGORY, x = 420, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 450, y = 364, hue = c.LABEL_HUE, text = "Next" })
end

function render.category_step(layout_ui, state, all_locations)
  local categories = d.categories_for_map(all_locations, state.map_id)

  if #categories == 0 then
    state.category = nil
  elseif state.category == nil or not d.contains(categories, state.category) then
    state.category = categories[1]
  end

  local total_pages
  state.page, total_pages = d.clamp_page(state.page, #categories, c.CATEGORY_ROWS)

  local start_idx = (state.page - 1) * c.CATEGORY_ROWS + 1
  local end_idx = math.min(#categories, start_idx + c.CATEGORY_ROWS - 1)
  state.visible_categories = {}

  ui.push(layout_ui, { type = "label", x = 24, y = 48, hue = c.TITLE_HUE, text = "Step 2/3 - Select category" })
  ui.push(layout_ui, { type = "label", x = 24, y = 64, hue = c.LABEL_HUE, text = "Map: " .. tostring(state.map_id) })

  local row = 1
  local y = 86
  for i = start_idx, end_idx do
    local category = categories[i]
    state.visible_categories[row] = category
    local prefix = (category == state.category) and "* " or "  "

    ui.push(layout_ui, { type = "button", id = c.BUTTON_CATEGORY_BASE + row, x = 22, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 50,
      y = y + 2,
      width = 430,
      height = 20,
      hue = c.LIST_HUE,
      text = d.trunc(prefix .. category, 60)
    })

    y = y + c.CATEGORY_ROW_HEIGHT
    row = row + 1
  end

  ui.add_page_nav(layout_ui, state.page, total_pages)
  ui.push(layout_ui, { type = "button", id = c.BUTTON_BACK_TO_MAP, x = 330, y = 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 360, y = 364, hue = c.LABEL_HUE, text = "Back" })

  ui.push(layout_ui, { type = "button", id = c.BUTTON_TO_LOCATION, x = 420, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 450, y = 364, hue = c.LABEL_HUE, text = "Next" })
end

function render.location_step(layout_ui, state, all_locations)
  local categories = d.categories_for_map(all_locations, state.map_id)

  if #categories == 0 then
    state.category = nil
    state.selected = nil
    state.visible_locations = {}
  elseif state.category == nil or not d.contains(categories, state.category) then
    state.category = categories[1]
  end

  local locations = {}
  if state.category ~= nil then
    locations = d.locations_for_filter(all_locations, state.map_id, state.category)
  end

  local total_pages
  state.page, total_pages = d.clamp_page(state.page, #locations, c.LOCATION_ROWS)

  local start_idx = (state.page - 1) * c.LOCATION_ROWS + 1
  local end_idx = math.min(#locations, start_idx + c.LOCATION_ROWS - 1)
  state.visible_locations = {}

  ui.push(layout_ui, { type = "label", x = 24, y = 48, hue = c.TITLE_HUE, text = "Step 3/3 - Select location" })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 24,
    y = 64,
    width = 460,
    height = 20,
    hue = c.LABEL_HUE,
    text = "Map: " .. tostring(state.map_id) .. "   Category: " .. tostring(state.category or "-")
  })

  local row = 1
  local y = 86
  for i = start_idx, end_idx do
    local location_entry = locations[i]
    state.visible_locations[row] = location_entry
    local prefix = d.selected_equals(state.selected, location_entry) and "* " or "  "

    ui.push(layout_ui, { type = "button", id = c.BUTTON_LOCATION_BASE + row, x = 22, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 50,
      y = y + 2,
      width = 430,
      height = 20,
      hue = c.LIST_HUE,
      text = d.trunc(prefix .. location_entry.name .. " (" .. location_entry.x .. "," .. location_entry.y .. "," .. location_entry.z .. ")", 60)
    })

    y = y + c.LOCATION_ROW_HEIGHT
    row = row + 1
  end

  ui.add_page_nav(layout_ui, state.page, total_pages)
  ui.push(layout_ui, { type = "button", id = c.BUTTON_BACK_TO_CATEGORY, x = 300, y = 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 330, y = 364, hue = c.LABEL_HUE, text = "Back" })

  ui.push(layout_ui, { type = "button", id = c.BUTTON_GO, x = 420, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 450, y = 364, hue = c.LABEL_HUE, text = "Go" })

  if state.selected ~= nil then
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 150,
      y = 364,
      width = 140,
      height = 20,
      hue = c.LABEL_HUE,
      text = d.trunc(state.selected.name, 18)
    })
  end
end

return render
