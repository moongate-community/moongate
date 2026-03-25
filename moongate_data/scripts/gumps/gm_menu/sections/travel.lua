local c = require("gumps.gm_menu.constants")
local ui = require("gumps.gm_menu.ui")

local travel_section = {}

function travel_section.add_content(layout_ui)
  ui.push(layout_ui, { type = "label", x = 196, y = 62, hue = c.TITLE_HUE, text = "Travel" })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 196,
    y = 88,
    width = 324,
    height = 20,
    hue = c.MUTED_HUE,
    text = "Curated travel destinations will appear here."
  })
end

return travel_section
