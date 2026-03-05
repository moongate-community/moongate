local c = require("gumps.teleports.constants")

local actions = {}

local function info(msg)
  if log ~= nil and log.info ~= nil then
    log.info(msg)
  end
end

function actions.apply_button_action(state, button_id, character)
  if button_id >= c.BUTTON_MAP_BASE + 1 and button_id <= c.BUTTON_MAP_BASE + c.MAP_ROWS then
    local row = button_id - c.BUTTON_MAP_BASE
    local selected_map = state.visible_maps[row]
    if selected_map ~= nil then
      state.map_id = selected_map.map_id
      state.category = nil
      state.selected = nil
    end
  elseif button_id >= c.BUTTON_CATEGORY_BASE + 1 and button_id <= c.BUTTON_CATEGORY_BASE + c.CATEGORY_ROWS then
    local row = button_id - c.BUTTON_CATEGORY_BASE
    local selected_category = state.visible_categories[row]
    if selected_category ~= nil then
      state.category = selected_category
      state.selected = nil
    end
  elseif button_id >= c.BUTTON_LOCATION_BASE + 1 and button_id <= c.BUTTON_LOCATION_BASE + c.LOCATION_ROWS then
    local row = button_id - c.BUTTON_LOCATION_BASE
    local selected_location = state.visible_locations[row]
    if selected_location ~= nil then
      state.selected = selected_location
    end
  elseif button_id == c.BUTTON_PREV_PAGE then
    state.page = state.page - 1
  elseif button_id == c.BUTTON_NEXT_PAGE then
    state.page = state.page + 1
  elseif button_id == c.BUTTON_TO_CATEGORY then
    state.view = "category"
    state.page = 1
  elseif button_id == c.BUTTON_TO_LOCATION then
    state.view = "location"
    state.page = 1
  elseif button_id == c.BUTTON_BACK_TO_MAP then
    state.view = "map"
    state.page = 1
  elseif button_id == c.BUTTON_BACK_TO_CATEGORY then
    state.view = "category"
    state.page = 1
  elseif button_id == c.BUTTON_REFRESH then
    state.page = 1
  elseif button_id == c.BUTTON_GO then
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
end

return actions
