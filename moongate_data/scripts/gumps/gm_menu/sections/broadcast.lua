local c = require("gumps.gm_menu.constants")
local ui = require("gumps.gm_menu.ui")
local header = require("gumps.layout.header")

local broadcast_section = {}

local function ensure_broadcast_state(current_state)
  if current_state.broadcast == nil then
    current_state.broadcast = {
      message = ""
    }
  end

  return current_state.broadcast
end

local function get_message(text_entries, fallback)
  if text_entries == nil then
    return fallback or ""
  end

  local value = text_entries[c.TEXT_ENTRY_BROADCAST_MESSAGE]
  if value == nil then
    return fallback or ""
  end

  return tostring(value)
end

local function trim(value)
  return (tostring(value or ""):gsub("^%s*(.-)%s*$", "%1"))
end

function broadcast_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
  local layout_ui = layout.ui
  local broadcast_state = ensure_broadcast_state(current_state)

  ui.push(layout_ui, { type = "image_tiled", x = 188, y = 48, width = 520, height = 428, gump_id = 2624 })
  local content_y = header.add(layout_ui, {
    x = 196,
    y = 62,
    width = 480,
    title = "Broadcast Message",
    subtitle = "Send a server-wide announcement with the fixed SERVER: prefix.",
    title_hue = c.TITLE_HUE,
    subtitle_hue = c.MUTED_HUE
  })

  ui.push(layout_ui, { type = "label", x = 206, y = content_y + 10, hue = c.LABEL_HUE, text = "Message" })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 206,
    y = content_y + 34,
    width = 470,
    height = 20,
    hue = c.MUTED_HUE,
    text = "SERVER:"
  })
  ui.push(layout_ui, {
    type = "text_entry_limited",
    x = 206,
    y = content_y + 58,
    width = 470,
    height = 20,
    hue = c.LABEL_HUE,
    entry_id = c.TEXT_ENTRY_BROADCAST_MESSAGE,
    text = tostring(broadcast_state.message or ""),
    size = 160
  })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 206,
    y = content_y + 94,
    width = 470,
    height = 36,
    hue = c.MUTED_HUE,
    text = "Use this for operational announcements. The prefix is applied automatically."
  })

  ui.push(layout_ui, { type = "button", id = c.BUTTON_BROADCAST_SEND, x = 206, y = content_y + 146, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 236, y = content_y + 148, hue = c.LABEL_HUE, text = "Send" })

  _ = session_id
  _ = character_id

  return function(ctx)
    local button_id = tonumber(ctx.button_id) or 0
    local state_for_session = ensure_broadcast_state(current_state)
    state_for_session.message = get_message(ctx.text_entries, state_for_session.message)

    if button_id ~= c.BUTTON_BROADCAST_SEND then
      return
    end

    local message = trim(state_for_session.message)

    if message == "" then
      speech.send(ctx.session_id, "Broadcast message cannot be empty.")
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    speech.broadcast("SERVER: " .. message)
    state_for_session.message = ""
    speech.send(ctx.session_id, "Broadcast sent.")
    reopen_callback(ctx.session_id, ctx.character_id)
  end
end

return broadcast_section
