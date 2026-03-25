local c = require("gumps.teleports.constants")
local d = require("gumps.teleports.data")
local s = require("gumps.teleports.state")
local ui = require("gumps.teleports.ui")
local render = require("gumps.teleports.render")
local actions = require("gumps.teleports.actions")

local controller = {}

function controller.build_layout(session_id, character_id, reopen_callback, options)
  local sender_serial = tonumber(character_id) or 0
  if sender_serial <= 0 then
    sender_serial = tonumber(session_id) or 1
  end

  local state = s.get(session_id)
  local all_locations = d.load_locations()
  local maps = d.distinct_maps(all_locations)
  local view = ui.create_view(options)

  local layout = { ui = {}, handlers = {} }
  local layout_ui = layout.ui

  ui.add_frame(layout_ui, view)

  if #maps == 0 then
    ui.push(layout_ui, { type = "label", x = (view.origin_x or 0) + 24, y = (view.origin_y or 0) + 54, hue = c.LABEL_HUE, text = "No locations loaded." })
    return layout, sender_serial
  end

  render.ensure_valid_map(state, maps)

  if state.view == "map" then
    render.map_step(layout_ui, state, maps, view)
  elseif state.view == "category" then
    render.category_step(layout_ui, state, all_locations, view)
  else
    render.location_step(layout_ui, state, all_locations, view)
  end

  layout.handlers.on_click = function(ctx)
    local target_session = ctx.session_id
    local target_character = ctx.character_id
    local button_id = tonumber(ctx.button_id) or 0
    local target_state = s.get(target_session)

    actions.apply_button_action(target_state, button_id, target_character)
    reopen_callback(target_session, target_character)
  end

  return layout, sender_serial
end

return controller
