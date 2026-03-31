local stack = require("gumps.layout.stack")
local ui = require("gumps.moongates.ui")
local data = require("moongates.data")

local render = {}

local GROUP_PITCH = 26
local DESTINATION_PITCH = 24

function render.build(layout_ui, constants, current_state)
  local groups = data.groups()
  local selected_group = data.find_group(groups, current_state.group_id)

  if selected_group == nil then
    selected_group = data.first_group(groups)
    if selected_group ~= nil then
      current_state.group_id = selected_group.id
    end
  end

  local start_y = ui.add_frame(layout_ui, constants)
  local groups_cursor = stack.cursor(start_y)
  local destinations_cursor = stack.cursor(start_y)

  for index, group in ipairs(groups) do
    local row_y = groups_cursor:add(20, GROUP_PITCH - 20)
    ui.add_group_button(
      layout_ui,
      constants,
      24,
      row_y,
      constants.BUTTON_GROUP_BASE + index,
      group.name,
      selected_group ~= nil and selected_group.id == group.id
    )
  end

  if selected_group ~= nil then
    for index, destination in ipairs(selected_group.destinations or {}) do
      local row_y = destinations_cursor:add(20, DESTINATION_PITCH - 20)
      ui.add_destination_button(
        layout_ui,
        constants,
        180,
        row_y,
        constants.BUTTON_DEST_BASE + index,
        destination.name
      )
    end
  else
    ui.push(layout_ui, { type = "label", x = 180, y = start_y, hue = 1102, text = "No destinations configured." })
  end

  return selected_group
end

return render
