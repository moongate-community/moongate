local state = {}

local session_state = {}

local function create_default_state()
  return {
    active_tab = "add"
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
