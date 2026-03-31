local c = require("gumps.moongates.constants")
local state = require("gumps.moongates.state")
local render = require("gumps.moongates.render")
local data = require("moongates.data")

local public_moongate = {}

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
