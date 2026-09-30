-- ==============================================================================
-- Moongate - scripts/items/light.lua
--
-- What it is for:
--   The item script of the lights: candles, candelabras, lanterns, lamp posts,
--   wall sconces and torches (item template decoration_light and the light
--   templates of templates/items). Double clicking an unlit light lights it:
--   its graphic goes to the lit one and it gets a light shape if it has none;
--   double clicking a lit one douses it. The graphics and sounds are ModernUO's.
--   A light with no unlit graphic, such as a brazier, stays as it is.
--
-- Props it reads:
--   protected  true for the lights ".decorate" places: only game masters and
--              administrators light or douse them
--   light      the light shape (a LightType name, such as circle150); kept when
--              the light is doused, so it lights again the same
--
-- Functions:
--   on_use(serial, user)             a player double clicks the light;
--                                    returns true
--   on_darkness(serial, dark)        a town lamp post's spot turned dark (true)
--                                    or light (false): the lamp post lights or
--                                    douses itself, silently
-- ==============================================================================

light = {}

-- Unlit graphic -> { lit graphic, the shape the lit one gives by default }, and
-- lit graphic -> unlit graphic. The tables are filled by add(): LuaCSharp does
-- not read a hexadecimal number between brackets, such as [0x0A27].
local LIGHTS = {}
local DOUSED = {}

local function add(unlit, lit, shape)
    LIGHTS[unlit] = { lit, shape }

    if DOUSED[lit] == nil then
        DOUSED[lit] = unlit
    end
end

add(0x0A27, 0x0B1D, "circle225") -- candelabra
add(0x0A29, 0x0B26, "circle225") -- candelabra stand
add(0x0A28, 0x0A0F, "circle150") -- candle
add(0x0A26, 0x0B1A, "circle150") -- large candle
add(0x1433, 0x1430, "circle150") -- long candle
add(0x142F, 0x142C, "circle150") -- short candle
add(0x1853, 0x1854, "circle150") -- skull with candle
add(0x1857, 0x1858, "circle150") -- skull with candle
add(0x0A1D, 0x0A1A, "circle300") -- hanging lantern
add(0x1849, 0x184A, "circle150") -- heating stand
add(0x0B21, 0x0B20, "circle300") -- lamp post
add(0x0B23, 0x0B22, "circle300") -- lamp post
add(0x0B25, 0x0B24, "circle300") -- lamp post
add(0x0A25, 0x0A22, "circle300") -- lantern
add(0x0A18, 0x0A22, "circle300") -- lantern
add(0x24BE, 0x24BD, "circle150") -- paper lantern
add(0x24CA, 0x24C9, "circle150") -- round paper lantern
add(0x24BC, 0x24BB, "circle150") -- shoji lantern
add(0x0F6B, 0x0A12, "circle300") -- torch
add(0x09FB, 0x09FD, "west_big") -- wall sconce, facing east
add(0x0A00, 0x0A02, "north_big") -- wall sconce, facing south
add(0x0A05, 0x0A07, "west_big") -- wall torch, facing east
add(0x0A0A, 0x0A0C, "north_big") -- wall torch, facing south
add(0x24C2, 0x24C1, "circle300") -- red hanging lantern
add(0x24C4, 0x24C3, "circle300") -- red hanging lantern
add(0x24C6, 0x24C5, "circle300") -- white hanging lantern
add(0x24C8, 0x24C7, "circle300") -- white hanging lantern

-- Lanterns lit another way douse to the plain lantern.
local function douses_to(lit, unlit)
    DOUSED[lit] = unlit
end

douses_to(0x0A15, 0x0A25)
douses_to(0x0A17, 0x0A25)

local LIGHT_SOUND = 0x47
local DOUSE_SOUND = 0x3BE

-- Called when a player double clicks the light.
function light.on_use(serial, user)
    if item.get_prop(serial, "protected") and not world.is_staff(user) then
        return true
    end

    local graphic = item.item_id(serial)
    local unlit = LIGHTS[graphic]

    -- set_item_id refuses a worn or held light: then nothing changes and nothing sounds.
    if unlit then
        if item.set_item_id(serial, unlit[1]) then
            if not item.get_prop(serial, "light") then
                item.set_light(serial, unlit[2])
            end

            item.play_sound(serial, LIGHT_SOUND)
        end
    elseif DOUSED[graphic] and item.set_item_id(serial, DOUSED[graphic]) then
        item.play_sound(serial, DOUSE_SOUND)
    end

    return true
end

-- Called for the town lamp posts when their spot turns dark or light.
function light.on_darkness(serial, dark)
    local graphic = item.item_id(serial)
    local unlit = LIGHTS[graphic]

    if dark and unlit then
        if item.set_item_id(serial, unlit[1]) and not item.get_prop(serial, "light") then
            item.set_light(serial, unlit[2])
        end
    elseif not dark and DOUSED[graphic] then
        item.set_item_id(serial, DOUSED[graphic])
    end
end
