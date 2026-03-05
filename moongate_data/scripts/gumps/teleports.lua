local teleports = {}

local GUMP_ID = 0xB61F
local GUMP_X = 80
local GUMP_Y = 60
local GUMP_WIDTH = 520
local GUMP_HEIGHT = 420
local LABEL_HUE = 33
local TITLE_HUE = 1152
local LIST_HUE = 1153

local MAP_ROWS = 10
local CATEGORY_ROWS = 12
local LOCATION_ROWS = 12

local MAP_ROW_HEIGHT = 22
local CATEGORY_ROW_HEIGHT = 22
local LOCATION_ROW_HEIGHT = 22

local BUTTON_REFRESH = 10
local BUTTON_GO = 15
local BUTTON_TO_CATEGORY = 20
local BUTTON_TO_LOCATION = 21
local BUTTON_BACK_TO_MAP = 22
local BUTTON_BACK_TO_CATEGORY = 23
local BUTTON_PREV_PAGE = 24
local BUTTON_NEXT_PAGE = 25

local BUTTON_MAP_BASE = 100
local BUTTON_CATEGORY_BASE = 200
local BUTTON_LOCATION_BASE = 300

local state_by_session = {}

local function info(msg)
  if log ~= nil and log.info ~= nil then
    log.info(msg)
  end
end

local function trunc(text, max_len)
  if text == nil then
    return ""
  end

  local s = tostring(text)
  if #s <= max_len then
    return s
  end

  return string.sub(s, 1, max_len - 3) .. "..."
end

local function load_locations()
  local list = {}
  local total = location.count()

  for i = 1, total do
    local e = location.get(i)
    if e ~= nil then
      list[#list + 1] = {
        map_id = tonumber(e.map_id) or 0,
        map_name = tostring(e.map_name or ("Map " .. tostring(tonumber(e.map_id) or 0))),
        category = tostring(e.category_path or "uncategorized"),
        name = tostring(e.name or "Unnamed"),
        x = tonumber(e.location_x) or 0,
        y = tonumber(e.location_y) or 0,
        z = tonumber(e.location_z) or 0
      }
    end
  end

  table.sort(list, function(a, b)
    if a.map_id ~= b.map_id then
      return a.map_id < b.map_id
    end
    if a.category ~= b.category then
      return a.category < b.category
    end

    return a.name < b.name
  end)

  return list
end

local function distinct_maps(list)
  local seen = {}
  local maps = {}

  for _, it in ipairs(list) do
    if not seen[it.map_id] then
      seen[it.map_id] = true
      maps[#maps + 1] = { map_id = it.map_id, map_name = it.map_name }
    end
  end

  table.sort(maps, function(a, b)
    if a.map_id ~= b.map_id then
      return a.map_id < b.map_id
    end

    return a.map_name < b.map_name
  end)

  return maps
end

local function categories_for_map(list, map_id)
  local seen = {}
  local categories = {}

  for _, it in ipairs(list) do
    if it.map_id == map_id and not seen[it.category] then
      seen[it.category] = true
      categories[#categories + 1] = it.category
    end
  end

  table.sort(categories)
  return categories
end

local function locations_for_filter(list, map_id, category)
  local result = {}

  for _, it in ipairs(list) do
    if it.map_id == map_id and it.category == category then
      result[#result + 1] = it
    end
  end

  table.sort(result, function(a, b)
    return a.name < b.name
  end)

  return result
end

local function clamp_page(page, total_items, page_size)
  local total_pages = math.max(1, math.ceil(total_items / page_size))
  if page < 1 then
    page = 1
  end
  if page > total_pages then
    page = total_pages
  end

  return page, total_pages
end

local function contains(t, value)
  for _, v in ipairs(t) do
    if v == value then
      return true
    end
  end

  return false
end

local function selected_equals(a, b)
  if a == nil or b == nil then
    return false
  end

  return a.map_id == b.map_id and a.x == b.x and a.y == b.y and a.z == b.z and a.name == b.name
end

local function get_state(session_id)
  local s = state_by_session[session_id]
  if s ~= nil then
    return s
  end

  s = {
    view = "map",
    map_id = nil,
    category = nil,
    page = 1,
    selected = nil,
    visible_maps = {},
    visible_categories = {},
    visible_locations = {}
  }

  state_by_session[session_id] = s
  return s
end

local function push(ui, entry)
  ui[#ui + 1] = entry
end

local function add_frame(ui)
  push(ui, { type = "background", x = 0, y = 0, gump_id = 5054, width = GUMP_WIDTH, height = GUMP_HEIGHT })
  push(ui, { type = "alpha_region", x = 10, y = 10, width = 500, height = 400 })

  push(ui, { type = "image_tiled", x = 10, y = 10, width = 500, height = 22, gump_id = 2624 })
  push(ui, { type = "alpha_region", x = 10, y = 10, width = 500, height = 22 })
  push(ui, { type = "label", x = 20, y = 14, hue = TITLE_HUE, text = "Teleport Browser" })

  push(ui, { type = "button", id = BUTTON_REFRESH, x = 420, y = 12, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(ui, { type = "label", x = 450, y = 14, hue = LABEL_HUE, text = "Refresh" })

  push(ui, { type = "image_tiled", x = 10, y = 40, width = 500, height = 300, gump_id = 2624 })
  push(ui, { type = "alpha_region", x = 10, y = 40, width = 500, height = 300 })

  push(ui, { type = "image_tiled", x = 10, y = 350, width = 500, height = 60, gump_id = 2624 })
  push(ui, { type = "alpha_region", x = 10, y = 350, width = 500, height = 60 })
end

local function add_page_nav(ui, page, pages)
  push(ui, { type = "button", id = BUTTON_PREV_PAGE, x = 20, y = 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
  push(ui, { type = "button", id = BUTTON_NEXT_PAGE, x = 60, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  push(ui, { type = "label", x = 95, y = 364, hue = LABEL_HUE, text = "Page " .. tostring(page) .. "/" .. tostring(pages) })
end

local function build_layout(session_id, character_id)
  local sender_serial = tonumber(character_id) or 0
  if sender_serial <= 0 then
    sender_serial = tonumber(session_id) or 1
  end

  local s = get_state(session_id)
  local all_locations = load_locations()
  local maps = distinct_maps(all_locations)

  local layout = { ui = {}, handlers = {} }
  local ui = layout.ui

  add_frame(ui)

  if #maps == 0 then
    push(ui, { type = "label", x = 24, y = 54, hue = LABEL_HUE, text = "No locations loaded." })
    return layout, sender_serial
  end

  if s.map_id == nil then
    s.map_id = maps[1].map_id
  end

  local map_ok = false
  for _, m in ipairs(maps) do
    if m.map_id == s.map_id then
      map_ok = true
      break
    end
  end

  if not map_ok then
    s.map_id = maps[1].map_id
    s.category = nil
    s.selected = nil
    s.page = 1
  end

  if s.view == "map" then
    local total_pages
    s.page, total_pages = clamp_page(s.page, #maps, MAP_ROWS)
    local start_idx = (s.page - 1) * MAP_ROWS + 1
    local end_idx = math.min(#maps, start_idx + MAP_ROWS - 1)

    s.visible_maps = {}

    push(ui, { type = "label", x = 24, y = 48, hue = TITLE_HUE, text = "Step 1/3 - Select map" })

    local row = 1
    local y = 72
    for i = start_idx, end_idx do
      local m = maps[i]
      s.visible_maps[row] = m
      local prefix = (m.map_id == s.map_id) and "* " or "  "

      push(ui, { type = "button", id = BUTTON_MAP_BASE + row, x = 22, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
      push(ui, { type = "label_cropped", x = 50, y = y + 2, width = 430, height = 20, hue = LIST_HUE, text = trunc(prefix .. m.map_name .. " (" .. tostring(m.map_id) .. ")", 58) })

      y = y + MAP_ROW_HEIGHT
      row = row + 1
    end

    add_page_nav(ui, s.page, total_pages)
    push(ui, { type = "button", id = BUTTON_TO_CATEGORY, x = 420, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    push(ui, { type = "label", x = 450, y = 364, hue = LABEL_HUE, text = "Next" })
  elseif s.view == "category" then
    local categories = categories_for_map(all_locations, s.map_id)
    if #categories == 0 then
      s.category = nil
    elseif s.category == nil or not contains(categories, s.category) then
      s.category = categories[1]
    end

    local total_pages
    s.page, total_pages = clamp_page(s.page, #categories, CATEGORY_ROWS)
    local start_idx = (s.page - 1) * CATEGORY_ROWS + 1
    local end_idx = math.min(#categories, start_idx + CATEGORY_ROWS - 1)

    s.visible_categories = {}

    push(ui, { type = "label", x = 24, y = 48, hue = TITLE_HUE, text = "Step 2/3 - Select category" })
    push(ui, { type = "label", x = 24, y = 64, hue = LABEL_HUE, text = "Map: " .. tostring(s.map_id) })

    local row = 1
    local y = 86
    for i = start_idx, end_idx do
      local c = categories[i]
      s.visible_categories[row] = c
      local prefix = (c == s.category) and "* " or "  "

      push(ui, { type = "button", id = BUTTON_CATEGORY_BASE + row, x = 22, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
      push(ui, { type = "label_cropped", x = 50, y = y + 2, width = 430, height = 20, hue = LIST_HUE, text = trunc(prefix .. c, 60) })

      y = y + CATEGORY_ROW_HEIGHT
      row = row + 1
    end

    add_page_nav(ui, s.page, total_pages)
    push(ui, { type = "button", id = BUTTON_BACK_TO_MAP, x = 330, y = 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
    push(ui, { type = "label", x = 360, y = 364, hue = LABEL_HUE, text = "Back" })

    push(ui, { type = "button", id = BUTTON_TO_LOCATION, x = 420, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    push(ui, { type = "label", x = 450, y = 364, hue = LABEL_HUE, text = "Next" })
  else
    local categories = categories_for_map(all_locations, s.map_id)
    if #categories == 0 then
      s.category = nil
      s.selected = nil
      s.visible_locations = {}
    elseif s.category == nil or not contains(categories, s.category) then
      s.category = categories[1]
    end

    local locations = {}
    if s.category ~= nil then
      locations = locations_for_filter(all_locations, s.map_id, s.category)
    end

    local total_pages
    s.page, total_pages = clamp_page(s.page, #locations, LOCATION_ROWS)
    local start_idx = (s.page - 1) * LOCATION_ROWS + 1
    local end_idx = math.min(#locations, start_idx + LOCATION_ROWS - 1)

    s.visible_locations = {}

    push(ui, { type = "label", x = 24, y = 48, hue = TITLE_HUE, text = "Step 3/3 - Select location" })
    push(ui, { type = "label_cropped", x = 24, y = 64, width = 460, height = 20, hue = LABEL_HUE, text = "Map: " .. tostring(s.map_id) .. "   Category: " .. tostring(s.category or "-") })

    local row = 1
    local y = 86
    for i = start_idx, end_idx do
      local loc = locations[i]
      s.visible_locations[row] = loc
      local prefix = selected_equals(s.selected, loc) and "* " or "  "

      push(ui, { type = "button", id = BUTTON_LOCATION_BASE + row, x = 22, y = y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
      push(ui, {
        type = "label_cropped",
        x = 50,
        y = y + 2,
        width = 430,
        height = 20,
        hue = LIST_HUE,
        text = trunc(prefix .. loc.name .. " (" .. loc.x .. "," .. loc.y .. "," .. loc.z .. ")", 60)
      })

      y = y + LOCATION_ROW_HEIGHT
      row = row + 1
    end

    add_page_nav(ui, s.page, total_pages)
    push(ui, { type = "button", id = BUTTON_BACK_TO_CATEGORY, x = 300, y = 362, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
    push(ui, { type = "label", x = 330, y = 364, hue = LABEL_HUE, text = "Back" })

    push(ui, { type = "button", id = BUTTON_GO, x = 420, y = 362, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    push(ui, { type = "label", x = 450, y = 364, hue = LABEL_HUE, text = "Go" })

    if s.selected ~= nil then
      push(ui, {
        type = "label_cropped",
        x = 150,
        y = 364,
        width = 140,
        height = 20,
        hue = LABEL_HUE,
        text = trunc(s.selected.name, 18)
      })
    end
  end

  local function on_click(ctx)
    local session = ctx.session_id
    local character = ctx.character_id
    local button_id = tonumber(ctx.button_id) or 0
    local state = get_state(session)

    if button_id >= BUTTON_MAP_BASE + 1 and button_id <= BUTTON_MAP_BASE + MAP_ROWS then
      local row = button_id - BUTTON_MAP_BASE
      local selected_map = state.visible_maps[row]
      if selected_map ~= nil then
        state.map_id = selected_map.map_id
        state.category = nil
        state.selected = nil
      end
    elseif button_id >= BUTTON_CATEGORY_BASE + 1 and button_id <= BUTTON_CATEGORY_BASE + CATEGORY_ROWS then
      local row = button_id - BUTTON_CATEGORY_BASE
      local selected_category = state.visible_categories[row]
      if selected_category ~= nil then
        state.category = selected_category
        state.selected = nil
      end
    elseif button_id >= BUTTON_LOCATION_BASE + 1 and button_id <= BUTTON_LOCATION_BASE + LOCATION_ROWS then
      local row = button_id - BUTTON_LOCATION_BASE
      local selected_location = state.visible_locations[row]
      if selected_location ~= nil then
        state.selected = selected_location
      end
    elseif button_id == BUTTON_PREV_PAGE then
      state.page = state.page - 1
    elseif button_id == BUTTON_NEXT_PAGE then
      state.page = state.page + 1
    elseif button_id == BUTTON_TO_CATEGORY then
      state.view = "category"
      state.page = 1
    elseif button_id == BUTTON_TO_LOCATION then
      state.view = "location"
      state.page = 1
    elseif button_id == BUTTON_BACK_TO_MAP then
      state.view = "map"
      state.page = 1
    elseif button_id == BUTTON_BACK_TO_CATEGORY then
      state.view = "category"
      state.page = 1
    elseif button_id == BUTTON_REFRESH then
      state.page = 1
    elseif button_id == BUTTON_GO then
      if state.selected ~= nil and character ~= nil then
        local m = mobile.get(character)
        if m ~= nil then
          info(
            "teleports go map="
              .. tostring(state.selected.map_id)
              .. " x="
              .. tostring(state.selected.x)
              .. " y="
              .. tostring(state.selected.y)
              .. " z="
              .. tostring(state.selected.z)
          )
          m:teleport(state.selected.map_id, state.selected.x, state.selected.y, state.selected.z)
        end
      end
    end

    teleports.open(session, character)
  end

  layout.handlers.on_click = on_click

  return layout, sender_serial
end

function teleports.open(session_id, character_id)
  local layout, sender_serial = build_layout(session_id, character_id)
  return gump.send_layout(session_id, layout, sender_serial, GUMP_ID, GUMP_X, GUMP_Y)
end

return teleports
