local public_moongate = require("gumps.moongates.public_moongate")

items_public_moongate = {}

local function is_in_range(source_item, actor)
  if source_item == nil or actor == nil then
    return false
  end

  if tonumber(source_item.map_id) ~= tonumber(actor.map_id) then
    return false
  end

  local dx = math.abs((tonumber(source_item.location_x) or 0) - (tonumber(actor.location_x) or 0))
  local dy = math.abs((tonumber(source_item.location_y) or 0) - (tonumber(actor.location_y) or 0))

  return math.max(dx, dy) <= 1
end

items_public_moongate.on_double_click = function(ctx)
  if
    ctx == nil or
    ctx.session_id == nil or
    ctx.mobile_id == nil or
    ctx.item == nil or
    ctx.item.serial == nil
  then
    return false
  end

  local source_item = item.get(ctx.item.serial)
  local actor = mobile.get(ctx.mobile_id)

  if not is_in_range(source_item, actor) then
    return false
  end

  return public_moongate.open(ctx.session_id, ctx.mobile_id, ctx.item.serial)
end
