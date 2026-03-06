local state = {}

local state_by_session = {}

function state.get(session_id)
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

return state
