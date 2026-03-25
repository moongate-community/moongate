local c = require("gumps.gm_menu.constants")
local ui = require("gumps.gm_menu.ui")

local add_section = {}

function add_section.add_content(layout_ui)
  ui.push(layout_ui, { type = "label", x = 196, y = 62, hue = c.TITLE_HUE, text = "Search Items and NPCs" })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 196,
    y = 88,
    width = 324,
    height = 20,
    hue = c.MUTED_HUE,
    text = "Use the sidebar to switch tools. Search and spawn controls are next."
  })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 196,
    y = 122,
    width = 324,
    height = 20,
    hue = c.LABEL_HUE,
    text = "Items and NPC templates will appear here."
  })
end

return add_section
