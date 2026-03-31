local data = {}

local function clone_destination(entry)
  return {
    id = tostring(entry.id or ""),
    name = tostring(entry.name or "Unnamed"),
    map = tostring(entry.map or ""),
    x = tonumber(entry.x) or 0,
    y = tonumber(entry.y) or 0,
    z = tonumber(entry.z) or 0
  }
end

local function clone_group(entry)
  local destinations = {}

  for _, destination in ipairs(entry.destinations or {}) do
    destinations[#destinations + 1] = clone_destination(destination)
  end

  return {
    id = tostring(entry.id or ""),
    name = tostring(entry.name or "Unknown"),
    destinations = destinations
  }
end

function data.load()
  return {
    {
      id = "britannia",
      name = "Britannia",
      destinations = {
        { id = "moonglow", name = "Moonglow", map = "felucca", x = 4467, y = 1283, z = 5 },
        { id = "britain", name = "Britain", map = "felucca", x = 1336, y = 1997, z = 5 },
        { id = "trinsic", name = "Trinsic", map = "felucca", x = 1828, y = 2948, z = -20 }
      }
    },
    {
      id = "ilshenar",
      name = "Ilshenar",
      destinations = {
        { id = "compassion", name = "Compassion", map = "ilshenar", x = 1215, y = 467, z = -13 },
        { id = "honor", name = "Honor", map = "ilshenar", x = 744, y = 724, z = -28 }
      }
    }
  }
end

function data.groups()
  local result = {}

  for _, entry in ipairs(data.load()) do
    result[#result + 1] = clone_group(entry)
  end

  return result
end

function data.first_group(groups)
  if groups == nil or #groups == 0 then
    return nil
  end

  return groups[1]
end

function data.find_group(groups, group_id)
  if groups == nil or group_id == nil then
    return nil
  end

  local normalized = tostring(group_id)

  for _, group in ipairs(groups) do
    if tostring(group.id) == normalized then
      return group
    end
  end

  return nil
end

return data
