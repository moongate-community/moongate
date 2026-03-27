local c = require("gumps.gm_menu.constants")
local state = require("gumps.gm_menu.state")
local ui = require("gumps.gm_menu.ui")
local render = require("gumps.gm_menu.render")

local controller = {}

function controller.build_layout(session_id, character_id, reopen_callback)
  local sender_serial = tonumber(character_id) or 0
  if sender_serial <= 0 then
    sender_serial = tonumber(session_id) or 1
  end

  local current_state = state.get(session_id)
  local layout = { ui = {}, handlers = {} }
  local layout_ui = layout.ui

  ui.add_frame(layout_ui)
  ui.add_sidebar(layout_ui, current_state)
  local section_handler = render.add_content(layout, session_id, character_id, current_state, reopen_callback)

  layout.handlers.on_click = function(ctx)
    local button_id = tonumber(ctx.button_id) or 0

    if button_id == c.BUTTON_TAB_TRAVEL then
      state.set_active_tab(ctx.session_id, "travel")
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_TAB_PROBE then
      state.set_active_tab(ctx.session_id, "probe")
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_TAB_ADD then
      state.set_active_tab(ctx.session_id, "add")
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if section_handler ~= nil then
      section_handler(ctx)
    end
  end

  return layout, sender_serial
end

return controller
