local c = require("gumps.gm_menu.constants")
local ui = require("gumps.gm_menu.ui")
local header = require("gumps.layout.header")
local stack = require("gumps.layout.stack")

local spawn_section = {}

local COMMANDS = {
  { button_id = c.BUTTON_SPAWN_BASE, label = "Spawn Doors", command = "spawn_doors" },
  { button_id = c.BUTTON_SPAWN_BASE + 1, label = "Spawn Signs", command = "spawn_signs" },
  { button_id = c.BUTTON_SPAWN_BASE + 2, label = "Spawn Decorations", command = "spawn_decorations" },
  { button_id = c.BUTTON_SPAWN_BASE + 3, label = "Create Spawners", command = "create_spawners" }
}

local function find_command(button_id)
  for _, entry in ipairs(COMMANDS) do
    if entry.button_id == button_id then
      return entry
    end
  end

  return nil
end

local function add_commands(layout_ui)
  local cursor = stack.cursor(144)

  for _, entry in ipairs(COMMANDS) do
    local row_y = cursor:peek()

    ui.push(layout_ui, { type = "button", id = entry.button_id, x = 206, y = row_y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 236,
      y = row_y,
      width = 214,
      height = 20,
      hue = c.LABEL_HUE,
      text = entry.label
    })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 236,
      y = row_y + 16,
      width = 214,
      height = 18,
      hue = c.MUTED_HUE,
      text = "." .. entry.command
    })

    cursor:advance(38)
  end
end

function spawn_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
  local layout_ui = layout.ui

  ui.push(layout_ui, { type = "image_tiled", x = 188, y = 48, width = 520, height = 428, gump_id = 2624 })
  local content_y = header.add(layout_ui, {
    x = 196,
    y = 62,
    width = 480,
    title = "Spawn Tools",
    subtitle = "Execute curated world generation commands from the GM menu.",
    title_hue = c.TITLE_HUE,
    subtitle_hue = c.MUTED_HUE
  })

  ui.push(layout_ui, { type = "image_tiled", x = 196, y = content_y, width = 496, height = 244, gump_id = 2624 })
  add_commands(layout_ui)

  ui.push(layout_ui, {
    type = "label_cropped",
    x = 206,
    y = content_y + 258,
    width = 470,
    height = 40,
    hue = c.MUTED_HUE,
    text = "These actions port the legacy spawn_tools gump into the GM menu without exposing arbitrary command execution."
  })

  _ = session_id
  _ = character_id
  _ = current_state

  return function(ctx)
    local button_id = tonumber(ctx.button_id) or 0
    local selected = find_command(button_id)

    if selected == nil then
      return
    end

    speech.send(ctx.session_id, "Executing ." .. selected.command .. " ...")
    local lines = command.execute(selected.command, 1)

    if lines ~= nil then
      for _, line in ipairs(lines) do
        if type(line) == "string" and line ~= "" then
          speech.send(ctx.session_id, line)
        end
      end
    end

    reopen_callback(ctx.session_id, ctx.character_id)
  end
end

return spawn_section
