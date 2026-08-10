-- The world's doors. An item template with `ScriptId: items.door` runs these hooks, and every door
-- the decoration corpus placed is built from one of the templates named below.
--
-- A door needs no state of its own. Its graphic says everything: UO lays each door style out in a
-- block of sixteen ids, eight facings of (closed, open), so the graphic alone gives both which way
-- the door hangs and whether it is standing open right now.
--
--     facing = (graphic - base) / 2        base is the template's own graphic
--     open   = (graphic - base) % 2 == 1
--     opened = closed + 1
--
-- Opening also swings the door out of its own doorway, by an offset that depends on the facing --
-- otherwise it would open into itself and still block the way.

local door = {
    id = "items.door",
}

-- Each template's own graphic is its class's base. Ported from ModernUO's door classes and checked
-- against all 944 doors in the decoration corpus: every one of them yields a facing of 0-7.
local BASE = {
    metal_door = 0x675,
    metal_door_2 = 0x6C5,
    barred_metal_door = 0x685,
    barred_metal_door_2 = 0x1FED,
    strong_wood_door = 0x6E5,
    dark_wood_door = 0x6A5,
    light_wood_door = 0x6D5,
    rattan_door = 0x695,
    secret_dungeon_door = 0x316,
    secret_wooden_door = 0x334,
    secret_stone_door_1 = 0x0E8,
    secret_stone_door_2 = 0x324,
    secret_stone_door_3 = 0x354,
}

-- Where a door of each facing swings to. From ModernUO's BaseDoor; index is facing + 1 because Lua
-- counts from one.
local OFFSET = {
    { x = -1, y = 1 },
    { x = 1, y = 1 },
    { x = -1, y = 0 },
    { x = 1, y = -1 },
    { x = 1, y = 1 },
    { x = 1, y = -1 },
    { x = 0, y = 0 },
    { x = 0, y = -1 },
}

-- Long enough to walk through, short enough that a town does not end the night wide open.
local CLOSE_AFTER_MS = 20000

--- Reads a door's graphic into what it means, or nil when this is not a door we know.
local function describe(template_id, item_id)
    local base = BASE[template_id]

    if base == nil then
        return nil
    end

    local delta = item_id - base

    if delta < 0 or delta > 15 then
        return nil
    end

    local facing = math.floor(delta / 2)

    return {
        open = delta % 2 == 1,
        offset = OFFSET[facing + 1],
    }
end

--- Swings the door: graphic one step, position one tile, in the direction its facing dictates.
local function swing(serial, item_id, x, y, z, state)
    if state.open then
        -- Closing: back to the closed graphic, and back to the doorway it came out of.
        item.set(serial, { item_id = item_id - 1 })
        item.move(serial, x - state.offset.x, y - state.offset.y, z)

        return
    end

    item.set(serial, { item_id = item_id + 1 })
    item.move(serial, x + state.offset.x, y + state.offset.y, z)

    -- A door left open is a door somebody forgot. Closing it costs one timer and no bookkeeping:
    -- the graphic is the state, so the callback re-reads it rather than trusting what it saw now.
    game.schedule(CLOSE_AFTER_MS, function()
        local now = item.get(serial)

        if now == nil then
            return
        end

        local current = describe(now.template_id, now.item_id)

        -- Somebody may have shut it already, or it may not be a door any more.
        if current == nil or not current.open then
            return
        end

        item.set(serial, { item_id = now.item_id - 1 })
        item.move(serial, now.x - current.offset.x, now.y - current.offset.y, now.z)
    end)
end

function door.on_double_click(ctx)
    local state = describe(ctx.item.template_id, ctx.item.item_id)

    -- Not a door this script knows: leave it alone rather than guess at its graphic.
    if state == nil then
        return
    end

    swing(ctx.item.serial, ctx.item.item_id, ctx.item.x, ctx.item.y, ctx.item.z, state)
end

return door
