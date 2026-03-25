local state = {}

local session_state = {}

local function create_default_add_state()
  return {
    filter = "items",
    query = "",
    page = 1,
    quantity = 1,
    selected = nil,
    brush = {
      active = false,
      kind = nil,
      template_id = nil,
      display_name = nil,
      item_id = 0,
      quantity = 1,
      cursor_id = 0,
      nonce = 0
    }
  }
end

local function create_default_state()
  return {
    active_tab = "add",
    add = create_default_add_state()
  }
end

function state.clear(session_id)
  local key = tonumber(session_id) or 0

  if key > 0 then
    session_state[key] = nil
  end
end

function state.get(session_id)
  local key = tonumber(session_id) or 0

  if key <= 0 then
    return create_default_state()
  end

  local current = session_state[key]

  if current == nil then
    current = create_default_state()
    session_state[key] = current
  end

  return current
end

function state.set_active_tab(session_id, active_tab)
  local current = state.get(session_id)

  if active_tab == "travel" then
    current.active_tab = "travel"
  else
    current.active_tab = "add"
  end

  return current
end

return state
