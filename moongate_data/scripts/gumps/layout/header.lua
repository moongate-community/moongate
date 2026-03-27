local header = {}

local DEFAULT_TITLE_HUE = 1152
local DEFAULT_SUBTITLE_HUE = 1102
local DEFAULT_WIDTH = 320
local DEFAULT_TITLE_HEIGHT = 20
local DEFAULT_SUBTITLE_HEIGHT = 28
local DEFAULT_TITLE_GAP = 4
local DEFAULT_AFTER_GAP = 10

local function push(layout_ui, entry)
  layout_ui[#layout_ui + 1] = entry
end

local function get_number(value, fallback)
  local parsed = tonumber(value)

  if parsed == nil then
    return fallback
  end

  return parsed
end

function header.add(layout_ui, options)
  local x = get_number(options.x, 0)
  local y = get_number(options.y, 0)
  local width = get_number(options.width, DEFAULT_WIDTH)
  local title_hue = get_number(options.title_hue, DEFAULT_TITLE_HUE)
  local subtitle_hue = get_number(options.subtitle_hue, DEFAULT_SUBTITLE_HUE)
  local title_height = get_number(options.title_height, DEFAULT_TITLE_HEIGHT)
  local subtitle_height = get_number(options.subtitle_height, DEFAULT_SUBTITLE_HEIGHT)
  local title_gap = get_number(options.title_gap, DEFAULT_TITLE_GAP)
  local after_gap = get_number(options.after_gap, DEFAULT_AFTER_GAP)
  local title = tostring(options.title or "")
  local subtitle = tostring(options.subtitle or "")

  push(layout_ui, {
    type = "label",
    x = x,
    y = y,
    hue = title_hue,
    text = title
  })

  local next_y = y + title_height

  if subtitle ~= "" then
    local subtitle_y = next_y + title_gap

    push(layout_ui, {
      type = "label_cropped",
      x = x,
      y = subtitle_y,
      width = width,
      height = subtitle_height,
      hue = subtitle_hue,
      text = subtitle
    })

    next_y = subtitle_y + subtitle_height
  end

  return next_y + after_gap
end

return header
