-- ==============================================================================
-- Moongate - scripts/items/moongate.lua
--
-- What it is for:
--   The item script of the plain moongate (item template moongate), as
--   ModernUO's Moongate: a gate with one destination. A player who walks onto
--   it, or double clicks it from the next cell, stands on the destination a
--   second later. Leaving a guarded place for one that is not asks first.
--
-- Props it reads:
--   teleport.x, teleport.y, teleport.z   the destination; a gate without all
--                                        three goes nowhere and says so
--   teleport.map   the destination map (a MapType number, or its name) when it
--                  is another one; without it the player stays on its map
--
-- Functions:
--   on_move_over(serial, who)   a player stepped onto the gate
--   on_use(serial, user)        a player double clicked the gate
-- ==============================================================================

moongate = {}

-- How far from the gate a player who double clicks it may stand, in cells.
local use_range = 1

-- Seconds between touching the gate and travelling.
local delay = 1

local warning_sound = 0x20E
local arrival_sound = 0x1FE

-- Server messages: "That is too far away." and "This moongate does not seem to go anywhere."
local too_far_message = 393
local nowhere_message = 30114

-- Client texts: "Gate Warning", "Dost thou wish to step into the moongate? Continue to enter the gate, Cancel to
-- stay here", "OKAY" and "CANCEL".
local warning_cliloc = 1062051
local question_cliloc = 1062049
local okay_cliloc = 1011036
local cancel_cliloc = 1011012

-- Where the player stands when it is on the gate's map and within range cells of it; nil otherwise.
local function near(serial, who, range)
    local here = item.location(serial)
    local at = mobile.location(who)

    if not here or not at or here.map ~= at.map then
        return nil
    end

    if math.abs(at.x - here.x) > range or math.abs(at.y - here.y) > range then
        return nil
    end

    return at
end

-- Who touched a gate and has not travelled yet: touching again meanwhile starts nothing.
local pending = {}

-- A whole number, as a prop set by hand may hold it as text; nil otherwise.
local function whole(value)
    local number = tonumber(value)

    if number and number == math.floor(number) then
        return number
    end

    return nil
end

-- The maps a gate may lead to; MapType cannot be walked with pairs.
local map_names = { "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur" }

-- The MapType of a number or of a name in any case, as a prop set by hand may hold it; nil for a map that does
-- not exist.
local function map_of(value)
    local number = whole(value)
    local name = type(value) == "string" and value:lower() or nil

    for _, known in ipairs(map_names) do
        local id = MapType[known]

        if id ~= nil and (id == number or known:lower() == name) then
            return id
        end
    end

    return nil
end

-- The destination as { x, y, z, map }, the map being the player's own when the gate names none; nil for a gate
-- that goes nowhere.
local function destination_of(serial, at)
    local x = whole(item.get_prop(serial, "teleport.x"))
    local y = whole(item.get_prop(serial, "teleport.y"))
    local z = whole(item.get_prop(serial, "teleport.z"))

    if not (x and y and z) then
        return nil
    end

    local map = item.get_prop(serial, "teleport.map")

    if map == nil then
        map = at.map
    else
        map = map_of(map)
    end

    if not map then
        return nil
    end

    return { x = x, y = y, z = z, map = map }
end

-- A map that is not loaded or a spot outside it refuses the player.
local function travel(who, destination)
    if mobile.teleport(who, destination.x, destination.y, destination.z, destination.map) then
        mobile.play_sound(who, arrival_sound)
    else
        mobile.message(who, localization.get(nowhere_message))
    end
end

-- As ModernUO: the player may have walked away while the gump was open.
local function confirmed(serial, who, destination)
    if not near(serial, who, use_range) then
        mobile.message(who, localization.get(too_far_message))

        return
    end

    travel(who, destination)
end

local function ask(serial, who, destination)
    -- Not the name of this script: a gump answers to the script table of its own id.
    local g = gump.create("moongate_warning", 110, 100)
    g:background{ x = 0, y = 0, gump = 5054, width = 420, height = 280 }
    g:image_tiled{ x = 10, y = 10, width = 400, height = 20, gump = 2624 }
    g:alpha_region{ x = 10, y = 10, width = 400, height = 20 }
    g:html{ x = 10, y = 10, width = 400, height = 20, cliloc = warning_cliloc, color = 30720 }
    g:image_tiled{ x = 10, y = 40, width = 400, height = 200, gump = 2624 }
    g:alpha_region{ x = 10, y = 40, width = 400, height = 200 }
    g:html{ x = 10, y = 40, width = 400, height = 200, cliloc = question_cliloc, color = 32512 }
    g:image_tiled{ x = 10, y = 250, width = 400, height = 20, gump = 2624 }
    g:alpha_region{ x = 10, y = 250, width = 400, height = 20 }
    g:button{ x = 10, y = 250, up = 4005, down = 4007, on_click = function(player)
        confirmed(serial, player, destination)
    end }
    g:html{ x = 40, y = 250, width = 170, height = 20, cliloc = okay_cliloc, color = 32767 }
    g:button{ x = 210, y = 250, up = 4005, down = 4007, on_click = function() end }
    g:html{ x = 240, y = 250, width = 170, height = 20, cliloc = cancel_cliloc, color = 32767 }

    if gump.send(who, g, {}) then
        mobile.play_sound(who, warning_sound)
    end
end

-- Called a second after the player touched the gate, from range cells at most.
local function arrive(serial, who, range)
    local at = near(serial, who, range)

    -- Gone, or the gate is: nothing to say.
    if not at then
        return
    end

    local destination = destination_of(serial, at)

    if not destination then
        mobile.message(who, localization.get(nowhere_message))

        return
    end

    -- As ModernUO: leaving the guards behind is worth a question.
    if world.is_guarded(at.map, at.x, at.y, at.z) and
        not world.is_guarded(destination.map, destination.x, destination.y, destination.z) then
        ask(serial, who, destination)
    else
        travel(who, destination)
    end
end

local function touch(serial, who, range)
    if pending[who] then
        return
    end

    pending[who] = true
    timer.after(delay, function()
        pending[who] = nil
        arrive(serial, who, range)
    end)
end

-- Called when a player steps onto the gate.
function moongate.on_move_over(serial, who)
    touch(serial, who, 0)
end

-- Called when a player double clicks the gate.
function moongate.on_use(serial, user)
    if near(serial, user, use_range) then
        touch(serial, user, use_range)
    else
        mobile.message(user, localization.get(too_far_message))
    end

    return true
end
