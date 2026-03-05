local data = {}

function data.trunc(text, max_len)
  if text == nil then
    return ""
  end

  local s = tostring(text)
  if #s <= max_len then
    return s
  end

  return string.sub(s, 1, max_len - 3) .. "..."
end

function data.load_locations()
  local list = {}
  local total = location.count()

  for i = 1, total do
    local e = location.get(i)
    if e ~= nil then
      list[#list + 1] = {
        map_id = tonumber(e.map_id) or 0,
        map_name = tostring(e.map_name or ("Map " .. tostring(tonumber(e.map_id) or 0))),
        category = tostring(e.category_path or "uncategorized"),
        name = tostring(e.name or "Unnamed"),
        x = tonumber(e.location_x) or 0,
        y = tonumber(e.location_y) or 0,
        z = tonumber(e.location_z) or 0
      }
    end
  end

  table.sort(list, function(a, b)
    if a.map_id ~= b.map_id then
      return a.map_id < b.map_id
    end
    if a.category ~= b.category then
      return a.category < b.category
    end

    return a.name < b.name
  end)

  return list
end

function data.distinct_maps(list)
  local seen = {}
  local maps = {}

  for _, it in ipairs(list) do
    if not seen[it.map_id] then
      seen[it.map_id] = true
      maps[#maps + 1] = { map_id = it.map_id, map_name = it.map_name }
    end
  end

  table.sort(maps, function(a, b)
    if a.map_id ~= b.map_id then
      return a.map_id < b.map_id
    end

    return a.map_name < b.map_name
  end)

  return maps
end

function data.categories_for_map(list, map_id)
  local seen = {}
  local categories = {}

  for _, it in ipairs(list) do
    if it.map_id == map_id and not seen[it.category] then
      seen[it.category] = true
      categories[#categories + 1] = it.category
    end
  end

  table.sort(categories)
  return categories
end

function data.locations_for_filter(list, map_id, category)
  local result = {}

  for _, it in ipairs(list) do
    if it.map_id == map_id and it.category == category then
      result[#result + 1] = it
    end
  end

  table.sort(result, function(a, b)
    return a.name < b.name
  end)

  return result
end

function data.clamp_page(page, total_items, page_size)
  local total_pages = math.max(1, math.ceil(total_items / page_size))
  if page < 1 then
    page = 1
  end
  if page > total_pages then
    page = total_pages
  end

  return page, total_pages
end

function data.contains(t, value)
  for _, v in ipairs(t) do
    if v == value then
      return true
    end
  end

  return false
end

function data.selected_equals(a, b)
  if a == nil or b == nil then
    return false
  end

  return a.map_id == b.map_id and a.x == b.x and a.y == b.y and a.z == b.z and a.name == b.name
end

return data
