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
      id = "felucca",
      name = "Felucca",
      destinations = {
        { id = "moonglow", name = "Moonglow", map = "felucca", x = 4467, y = 1283, z = 5 },
        { id = "britain", name = "Britain", map = "felucca", x = 1336, y = 1997, z = 5 },
        { id = "jhelom", name = "Jhelom", map = "felucca", x = 1499, y = 3771, z = 5 },
        { id = "yew", name = "Yew", map = "felucca", x = 771, y = 752, z = 5 },
        { id = "minoc", name = "Minoc", map = "felucca", x = 2701, y = 692, z = 5 },
        { id = "trinsic", name = "Trinsic", map = "felucca", x = 1828, y = 2948, z = -20 },
        { id = "skara_brae", name = "Skara Brae", map = "felucca", x = 643, y = 2067, z = 5 },
        { id = "buccaneers_den", name = "Buccaneer's Den", map = "felucca", x = 2711, y = 2234, z = 0 }
      }
    },
    {
      id = "ilshenar",
      name = "Ilshenar",
      destinations = {
        { id = "compassion", name = "Compassion", map = "ilshenar", x = 1215, y = 467, z = -13 },
        { id = "honesty", name = "Honesty", map = "ilshenar", x = 722, y = 1366, z = -60 },
        { id = "honor", name = "Honor", map = "ilshenar", x = 744, y = 724, z = -28 },
        { id = "humility", name = "Humility", map = "ilshenar", x = 281, y = 1016, z = 0 },
        { id = "justice", name = "Justice", map = "ilshenar", x = 987, y = 1011, z = -32 },
        { id = "sacrifice", name = "Sacrifice", map = "ilshenar", x = 1174, y = 1286, z = -30 },
        { id = "spirituality", name = "Spirituality", map = "ilshenar", x = 1532, y = 1340, z = -3 },
        { id = "valor", name = "Valor", map = "ilshenar", x = 528, y = 216, z = -45 },
        { id = "chaos", name = "Chaos", map = "ilshenar", x = 1721, y = 218, z = 96 }
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
