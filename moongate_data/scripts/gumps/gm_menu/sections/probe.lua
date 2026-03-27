local c = require("gumps.gm_menu.constants")
local ui = require("gumps.gm_menu.ui")

local probe_section = {}

local SAMPLE_ROWS = {
  {
    name = "Extremely Long Decorative Plate Legs East",
    meta = "decorative_plate_legs_east • 0x2B6D"
  },
  {
    name = "Zombie Shipwright Veteran",
    meta = "zombie_shipwright_veteran"
  }
}

local function add_sample_rows(layout_ui, panel_x, panel_y, inner_line_gap, row_pitch)
  local row_y = panel_y

  for _, sample in ipairs(SAMPLE_ROWS) do
    ui.push(layout_ui, {
      type = "label_cropped",
      x = panel_x,
      y = row_y,
      width = 126,
      height = 18,
      hue = c.LABEL_HUE,
      text = sample.name
    })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = panel_x,
      y = row_y + inner_line_gap,
      width = 126,
      height = 18,
      hue = c.MUTED_HUE,
      text = sample.meta
    })

    row_y = row_y + row_pitch
  end
end

local function add_probe_panel(layout_ui, x, title, subtitle, inner_line_gap, row_pitch)
  ui.push(layout_ui, { type = "image_tiled", x = x, y = 112, width = 150, height = 312, gump_id = 2624 })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = x + 10,
    y = 124,
    width = 130,
    height = 20,
    hue = c.ACCENT_HUE,
    text = title
  })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = x + 10,
    y = 144,
    width = 130,
    height = 28,
    hue = c.MUTED_HUE,
    text = subtitle
  })

  add_sample_rows(layout_ui, x + 10, 186, inner_line_gap, row_pitch)
end

function probe_section.add_content(layout, session_id, character_id, reopen_callback)
  local layout_ui = layout.ui

  ui.push(layout_ui, { type = "image_tiled", x = 188, y = 48, width = 520, height = 428, gump_id = 2624 })
  ui.push(layout_ui, { type = "label", x = 196, y = 62, hue = c.TITLE_HUE, text = "Spacing Probe" })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 196,
    y = 72,
    width = 496,
    height = 28,
    hue = c.MUTED_HUE,
    text = "Compare result row spacing in-client before changing the production Add tab rhythm."
  })

  add_probe_panel(layout_ui, 196, "Compact 16/36", "Tight but still readable.", 16, 36)
  add_probe_panel(layout_ui, 362, "Balanced 16/38", "Closer to staff/admin gump cadence.", 16, 38)
  add_probe_panel(layout_ui, 528, "Relaxed 18/40", "Most breathable option.", 18, 40)

  _ = session_id
  _ = character_id
  _ = reopen_callback

  return function()
  end
end

return probe_section
