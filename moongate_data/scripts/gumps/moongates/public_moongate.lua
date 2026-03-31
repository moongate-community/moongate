local c = require("gumps.moongates.constants")
local state = require("gumps.moongates.state")
local render = require("gumps.moongates.render")
local data = require("moongates.data")

local public_moongate = {}

local function is_in_range(source_item, actor)
  if source_item == nil or actor == nil then
    return false
  end

  if tonumber(source_item.map_id) ~= tonumber(actor.map_id) then
    return false
  end

  local dx = math.abs((tonumber(source_item.location_x) or 0) - (tonumber(actor.location_x) or 0))
  local dy = math.abs((tonumber(source_item.location_y) or 0) - (tonumber(actor.location_y) or 0))

  return math.max(dx, dy) <= 1
end

local function is_same_destination(actor, destination_map_id, destination)
  if actor == nil or destination == nil then
    return false
  end

  return tonumber(actor.map_id) == tonumber(destination_map_id) and
         tonumber(actor.location_x) == tonumber(destination.x) and
         tonumber(actor.location_y) == tonumber(destination.y) and
         tonumber(actor.location_z) == tonumber(destination.z)
end

local function send_effect_if_available(map_id, x, y, z)
  if effect == nil then
    return
  end

  effect.send(map_id, x, y, z, c.TELEPORT_EFFECT_ITEM_ID, 10, 10)
end

local function try_travel(session_id, character_id, current_state, destination_index)
  local groups = data.groups()
  local selected_group = data.find_group(groups, current_state.group_id)
  if selected_group == nil then
    return false
  end

  local destination = selected_group.destinations[destination_index]
  if destination == nil then
    return false
  end

  local actor = mobile.get(character_id)
  local source_item = item.get(current_state.source_item_serial)
  if not is_in_range(source_item, actor) then
    return false
  end

  local destination_map_id = map.to_id(destination.map, tonumber(actor.map_id) or 0)
  if destination_map_id < 0 then
    return false
  end

  if is_same_destination(actor, destination_map_id, destination) then
    return false
  end

  send_effect_if_available(tonumber(actor.map_id) or 0, tonumber(actor.location_x) or 0, tonumber(actor.location_y) or 0, tonumber(actor.location_z) or 0)

  if not actor:teleport(destination_map_id, destination.x, destination.y, destination.z) then
    return false
  end

  send_effect_if_available(destination_map_id, destination.x, destination.y, destination.z)
  actor:play_sound(c.TELEPORT_SOUND_ID)
  state.clear(session_id)

  return true
end

local function build_layout(session_id, item_serial)
  local current_state = state.set_source(session_id, item_serial)
  local layout = { ui = {}, handlers = {} }

  render.build(layout.ui, c, current_state)

  layout.handlers.on_click = function(ctx)
    local button_id = tonumber(ctx.button_id) or 0

    if button_id == c.BUTTON_CLOSE then
      state.clear(ctx.session_id)
      return false
    end

    if button_id >= c.BUTTON_GROUP_BASE and button_id < c.BUTTON_DEST_BASE then
      local groups = data.groups()
      local group_index = button_id - c.BUTTON_GROUP_BASE + 1
      local group = groups[group_index]

      if group ~= nil then
        state.set_group(ctx.session_id, group.id)
      end

      return public_moongate.open(ctx.session_id, ctx.character_id, current_state.source_item_serial)
    end

    if button_id >= c.BUTTON_DEST_BASE then
      local destination_index = button_id - c.BUTTON_DEST_BASE

      return try_travel(ctx.session_id, ctx.character_id, current_state, destination_index)
    end

    return false
  end

  return layout
end

function public_moongate.open(session_id, character_id, item_serial)
  if session_id == nil or character_id == nil or item_serial == nil then
    return false
  end

  local layout = build_layout(session_id, item_serial)

  return gump.send_layout(session_id, layout, character_id, c.GUMP_ID, c.GUMP_X, c.GUMP_Y)
end

return public_moongate
