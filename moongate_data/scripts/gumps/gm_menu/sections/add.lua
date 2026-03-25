local c = require("gumps.gm_menu.constants")
local ui = require("gumps.gm_menu.ui")
local gm_state = require("gumps.gm_menu.state")

local add_section = {}

local function create_default_add_state()
  return {
    filter = "items",
    query = "",
    page = 1,
    quantity = 1,
    selected = nil,
    brush = {
      active = false,
      kind = nil,
      template_id = nil,
      display_name = nil,
      item_id = 0,
      quantity = 1,
      cursor_id = 0,
      nonce = 0
    }
  }
end

local function ensure_add_state(current_state)
  if current_state.add == nil then
    current_state.add = create_default_add_state()
  end

  if current_state.add.brush == nil then
    current_state.add.brush = create_default_add_state().brush
  end

  return current_state.add
end

local function get_entry_text(text_entries, entry_id, fallback)
  if text_entries == nil then
    return fallback
  end

  local value = text_entries[entry_id]
  if value == nil then
    return fallback
  end

  return tostring(value)
end

local function get_kind(add_state)
  if add_state.filter == "npcs" then
    return "npc"
  end

  return "item"
end

local function get_quantity_limit(kind)
  if kind == "npc" then
    return c.MAX_NPC_QUANTITY
  end

  return c.MAX_ITEM_QUANTITY
end

local function clamp_quantity(raw_value, kind)
  local parsed = tonumber(raw_value) or 1
  local whole = math.floor(parsed)

  if whole < 1 then
    whole = 1
  end

  local max_quantity = get_quantity_limit(kind)
  if whole > max_quantity then
    whole = max_quantity
  end

  return whole
end

local function sync_inputs(add_state, text_entries)
  add_state.query = get_entry_text(text_entries, c.TEXT_ENTRY_SEARCH, add_state.query or "")
  add_state.quantity = clamp_quantity(get_entry_text(text_entries, c.TEXT_ENTRY_QUANTITY, add_state.quantity or 1), get_kind(add_state))
end

local function normalize_results(raw_results, kind)
  local results = {}

  for index = 1, c.RESULT_ROWS do
    local entry = raw_results[index]
    if entry == nil then
      break
    end

    results[#results + 1] = {
      kind = kind,
      template_id = tostring(entry.template_id or ""),
      display_name = tostring(entry.display_name or entry.template_id or ""),
      item_id = tonumber(entry.item_id) or 0
    }
  end

  return results
end

local function load_results(add_state)
  local kind = get_kind(add_state)
  local query = add_state.query or ""
  local page = tonumber(add_state.page) or 1
  local raw_results

  if kind == "item" then
    raw_results = item.search_templates(query, page, c.PAGE_SIZE)
  else
    raw_results = mobile.search_templates(query, page, c.PAGE_SIZE)
  end

  return normalize_results(raw_results, kind)
end

local function format_item_id(item_id)
  return string.format("0x%04X", tonumber(item_id) or 0)
end

local function clear_selection(add_state)
  add_state.selected = nil
end

local function clear_brush(add_state, session_id)
  local brush = add_state.brush

  if brush ~= nil and brush.active and (tonumber(brush.cursor_id) or 0) > 0 then
    target.cancel(session_id, brush.cursor_id)
  end

  add_state.brush = {
    active = false,
    kind = nil,
    template_id = nil,
    display_name = nil,
    item_id = 0,
    quantity = add_state.quantity or 1,
    cursor_id = 0,
    nonce = (brush ~= nil and tonumber(brush.nonce) or 0) + 1
  }
end

local function spawn_selection(selection, location, quantity)
  if selection == nil or location == nil then
    return false
  end

  if selection.kind == "item" then
    return item.spawn(selection.template_id, location, quantity) ~= nil
  end

  for i = 1, quantity do
    if mobile.spawn(selection.template_id, location) == nil then
      return false
    end
  end

  return true
end

local function request_spawn_target(session_id, character_id, selection, quantity, reopen_callback)
  return target.request_location(session_id, function(target_ctx)
    if target_ctx.cancelled then
      reopen_callback(session_id, character_id)
      return
    end

    spawn_selection(
      selection,
      {
        x = target_ctx.x,
        y = target_ctx.y,
        z = target_ctx.z,
        map_id = target_ctx.map_id
      },
      quantity
    )

    reopen_callback(session_id, character_id)
  end)
end

local request_brush_target

request_brush_target = function(session_id, character_id, nonce, reopen_callback)
  local current_state = gm_state.get(session_id)
  local add_state = ensure_add_state(current_state)
  local brush = add_state.brush

  brush.cursor_id = target.request_location(session_id, function(target_ctx)
    local latest_state = gm_state.get(session_id)
    local latest_add_state = ensure_add_state(latest_state)
    local latest_brush = latest_add_state.brush

    if target_ctx.cancelled then
      clear_brush(latest_add_state, session_id)
      reopen_callback(session_id, character_id)
      return
    end

    if latest_brush == nil or not latest_brush.active or latest_brush.nonce ~= nonce or latest_brush.template_id == nil then
      return
    end

    spawn_selection(
      {
        kind = latest_brush.kind,
        template_id = latest_brush.template_id,
        display_name = latest_brush.display_name,
        item_id = latest_brush.item_id or 0
      },
      {
        x = target_ctx.x,
        y = target_ctx.y,
        z = target_ctx.z,
        map_id = target_ctx.map_id
      },
      latest_brush.quantity or 1
    )

    reopen_callback(session_id, character_id)

    local refreshed_state = gm_state.get(session_id)
    local refreshed_add_state = ensure_add_state(refreshed_state)
    local refreshed_brush = refreshed_add_state.brush

    if refreshed_brush ~= nil and refreshed_brush.active and refreshed_brush.nonce == nonce then
      request_brush_target(session_id, character_id, nonce, reopen_callback)
    end
  end)

  return brush.cursor_id
end

local function activate_brush(add_state, selection, quantity)
  local brush = add_state.brush
  local next_nonce = (tonumber(brush.nonce) or 0) + 1

  brush.active = true
  brush.kind = selection.kind
  brush.template_id = selection.template_id
  brush.display_name = selection.display_name
  brush.item_id = selection.item_id or 0
  brush.quantity = quantity
  brush.cursor_id = 0
  brush.nonce = next_nonce

  return next_nonce
end

local function render_filter_buttons(layout_ui, add_state)
  local items_hue = add_state.filter == "items" and c.ACCENT_HUE or c.LABEL_HUE
  local npcs_hue = add_state.filter == "npcs" and c.ACCENT_HUE or c.LABEL_HUE

  ui.push(layout_ui, { type = "button", id = c.BUTTON_FILTER_ITEMS, x = 196, y = 86, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 226, y = 88, hue = items_hue, text = "Items" })

  ui.push(layout_ui, { type = "button", id = c.BUTTON_FILTER_NPCS, x = 300, y = 86, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 330, y = 88, hue = npcs_hue, text = "NPCs" })
end

local function render_results(layout_ui, add_state, results)
  ui.push(layout_ui, { type = "image_tiled", x = 196, y = 150, width = 260, height = 286, gump_id = 2624 })
  ui.push(layout_ui, { type = "label", x = 208, y = 160, hue = c.TITLE_HUE, text = "Results" })

  local row_y = 188

  for index, result in ipairs(results) do
    local button_id = c.BUTTON_RESULT_BASE + index - 1
    local is_selected = add_state.selected ~= nil and add_state.selected.kind == result.kind and add_state.selected.template_id == result.template_id
    local display_hue = is_selected and c.ACCENT_HUE or c.LABEL_HUE
    local meta_text = result.template_id

    if result.kind == "item" and result.item_id > 0 then
      meta_text = result.template_id .. " • " .. format_item_id(result.item_id)
    end

    ui.push(layout_ui, { type = "button", id = button_id, x = 206, y = row_y, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 236,
      y = row_y,
      width = 208,
      height = 18,
      hue = display_hue,
      text = result.display_name
    })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 236,
      y = row_y + 14,
      width = 208,
      height = 18,
      hue = c.MUTED_HUE,
      text = meta_text
    })

    row_y = row_y + 34
  end

  if #results == 0 then
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 208,
      y = 192,
      width = 228,
      height = 20,
      hue = c.MUTED_HUE,
      text = "No results. Try another query."
    })
  end
end

local function render_preview(layout_ui, add_state)
  local selected = add_state.selected

  ui.push(layout_ui, { type = "image_tiled", x = 470, y = 150, width = 224, height = 286, gump_id = 2624 })
  ui.push(layout_ui, { type = "label", x = 482, y = 160, hue = c.TITLE_HUE, text = "Preview" })

  if selected == nil then
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 482,
      y = 190,
      width = 188,
      height = 40,
      hue = c.MUTED_HUE,
      text = "Select an item or NPC template to inspect and spawn."
    })
    return
  end

  ui.push(layout_ui, {
    type = "label_cropped",
    x = 482,
    y = 190,
    width = 188,
    height = 20,
    hue = c.ACCENT_HUE,
    text = selected.display_name
  })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 482,
    y = 212,
    width = 188,
    height = 20,
    hue = c.LABEL_HUE,
    text = "Template: " .. selected.template_id
  })

  if selected.kind == "item" then
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 482,
      y = 234,
      width = 188,
      height = 20,
      hue = c.LABEL_HUE,
      text = "Item ID: " .. format_item_id(selected.item_id)
    })

    if selected.item_id > 0 then
      ui.push(layout_ui, { type = "item", x = 552, y = 272, item_id = selected.item_id })
    end
  else
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 482,
      y = 234,
      width = 188,
      height = 40,
      hue = c.MUTED_HUE,
      text = "NPC preview uses a placeholder in this first version."
    })
  end
end

local function render_actions(layout_ui, add_state)
  ui.push(layout_ui, { type = "label", x = 196, y = 116, hue = c.LABEL_HUE, text = "Search" })
  ui.push(layout_ui, {
    type = "text_entry_limited",
    x = 248,
    y = 112,
    width = 190,
    height = 20,
    hue = c.LABEL_HUE,
    entry_id = c.TEXT_ENTRY_SEARCH,
    text = add_state.query or "",
    size = 60
  })
  ui.push(layout_ui, { type = "button", id = c.BUTTON_SEARCH, x = 448, y = 112, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 478, y = 114, hue = c.LABEL_HUE, text = "Search" })

  ui.push(layout_ui, { type = "label", x = 560, y = 116, hue = c.LABEL_HUE, text = "Qty" })
  ui.push(layout_ui, {
    type = "text_entry_limited",
    x = 592,
    y = 112,
    width = 48,
    height = 20,
    hue = c.LABEL_HUE,
    entry_id = c.TEXT_ENTRY_QUANTITY,
    text = tostring(add_state.quantity or 1),
    size = 3
  })

  ui.push(layout_ui, { type = "button", id = c.BUTTON_PREV_PAGE, x = 196, y = 446, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 226, y = 448, hue = c.LABEL_HUE, text = "Prev" })
  ui.push(layout_ui, { type = "button", id = c.BUTTON_NEXT_PAGE, x = 290, y = 446, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 320, y = 448, hue = c.LABEL_HUE, text = "Next" })

  ui.push(layout_ui, { type = "button", id = c.BUTTON_TARGET_GROUND, x = 470, y = 446, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
  ui.push(layout_ui, { type = "label", x = 500, y = 448, hue = c.LABEL_HUE, text = "Target Ground" })

  if add_state.selected ~= nil and add_state.selected.kind == "item" then
    ui.push(layout_ui, { type = "button", id = c.BUTTON_ADD_TO_BACKPACK, x = 470, y = 418, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, { type = "label", x = 500, y = 420, hue = c.LABEL_HUE, text = "Add To Backpack" })
  end

  if add_state.brush.active then
    ui.push(layout_ui, { type = "button", id = c.BUTTON_STOP_BRUSH, x = 610, y = 446, normal_id = 4014, pressed_id = 4016, onclick = "on_click" })
    ui.push(layout_ui, { type = "label", x = 640, y = 448, hue = c.ACCENT_HUE, text = "Stop Brush" })
    ui.push(layout_ui, {
      type = "label_cropped",
      x = 470,
      y = 392,
      width = 204,
      height = 20,
      hue = c.ACCENT_HUE,
      text = "Brush Active: " .. tostring(add_state.brush.display_name or add_state.brush.template_id or "")
    })
  else
    ui.push(layout_ui, { type = "button", id = c.BUTTON_BRUSH, x = 610, y = 446, normal_id = 4005, pressed_id = 4007, onclick = "on_click" })
    ui.push(layout_ui, { type = "label", x = 640, y = 448, hue = c.LABEL_HUE, text = "Brush" })
  end
end

function add_section.add_content(layout, session_id, character_id, current_state, reopen_callback)
  local layout_ui = layout.ui
  local add_state = ensure_add_state(current_state)
  local results = load_results(add_state)

  ui.push(layout_ui, { type = "image_tiled", x = 188, y = 48, width = 520, height = 428, gump_id = 2624 })
  ui.push(layout_ui, { type = "label", x = 196, y = 62, hue = c.TITLE_HUE, text = "Search Items and NPCs" })
  ui.push(layout_ui, {
    type = "label_cropped",
    x = 196,
    y = 72,
    width = 480,
    height = 18,
    hue = c.MUTED_HUE,
    text = "Free search with preview, backpack add, ground target and brush."
  })

  render_filter_buttons(layout_ui, add_state)
  render_actions(layout_ui, add_state)
  render_results(layout_ui, add_state, results)
  render_preview(layout_ui, add_state)

  _ = session_id
  _ = character_id

  return function(ctx)
    local button_id = tonumber(ctx.button_id) or 0
    local state_for_session = gm_state.get(ctx.session_id)
    local current_add_state = ensure_add_state(state_for_session)
    local target_character_id = tonumber(ctx.character_id) or 0

    sync_inputs(current_add_state, ctx.text_entries)

    if button_id == c.BUTTON_FILTER_ITEMS then
      current_add_state.filter = "items"
      current_add_state.page = 1
      clear_selection(current_add_state)
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_FILTER_NPCS then
      current_add_state.filter = "npcs"
      current_add_state.page = 1
      clear_selection(current_add_state)
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_SEARCH then
      current_add_state.page = 1
      clear_selection(current_add_state)
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_PREV_PAGE then
      current_add_state.page = math.max(1, (tonumber(current_add_state.page) or 1) - 1)
      clear_selection(current_add_state)
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_NEXT_PAGE then
      current_add_state.page = (tonumber(current_add_state.page) or 1) + 1
      clear_selection(current_add_state)
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id >= c.BUTTON_RESULT_BASE and button_id < c.BUTTON_RESULT_BASE + c.RESULT_ROWS then
      local row = button_id - c.BUTTON_RESULT_BASE + 1
      local current_results = load_results(current_add_state)
      current_add_state.selected = current_results[row]
      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_ADD_TO_BACKPACK then
      if current_add_state.selected ~= nil and current_add_state.selected.kind == "item" and target_character_id > 0 then
        mobile.add_item_to_backpack(target_character_id, current_add_state.selected.template_id, current_add_state.quantity or 1)
      end

      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_TARGET_GROUND then
      if current_add_state.selected ~= nil and target_character_id > 0 then
        request_spawn_target(
          ctx.session_id,
          target_character_id,
          current_add_state.selected,
          current_add_state.quantity or 1,
          reopen_callback
        )
      end

      return
    end

    if button_id == c.BUTTON_BRUSH then
      if current_add_state.selected ~= nil and target_character_id > 0 then
        local nonce = activate_brush(current_add_state, current_add_state.selected, current_add_state.quantity or 1)
        request_brush_target(ctx.session_id, target_character_id, nonce, reopen_callback)
      end

      reopen_callback(ctx.session_id, ctx.character_id)
      return
    end

    if button_id == c.BUTTON_STOP_BRUSH then
      clear_brush(current_add_state, ctx.session_id)
      reopen_callback(ctx.session_id, ctx.character_id)
    end
  end
end

return add_section
