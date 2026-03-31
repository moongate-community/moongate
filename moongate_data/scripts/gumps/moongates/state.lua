local state = {}

local sessions = {}

function state.get(session_id)
  local key = tostring(tonumber(session_id) or 0)

  if sessions[key] == nil then
    sessions[key] = {
      group_id = nil,
      source_item_serial = 0
    }
  end

  return sessions[key]
end

function state.set_source(session_id, item_serial)
  local current = state.get(session_id)
  current.source_item_serial = tonumber(item_serial) or 0

  return current
end

function state.set_group(session_id, group_id)
  local current = state.get(session_id)
  current.group_id = group_id == nil and nil or tostring(group_id)

  return current
end

function state.clear(session_id)
  sessions[tostring(tonumber(session_id) or 0)] = nil
end

return state
