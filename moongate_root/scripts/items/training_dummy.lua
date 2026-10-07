-- ==============================================================================
-- Moongate - scripts/items/training_dummy.lua
--
-- What it is for:
--   The item script of the training dummies (0x1070 and 0x1071 facing south,
--   0x1074 and 0x1075 facing east), as ModernUO's TrainingDummy: a player with a
--   melee weapon in hand, or none, double clicks the dummy and swings at it, and
--   the skill of the weapon may rise, up to 25 points. A template uses it with
--   script_id = "training_dummy".
--
--   The dummy swings back for three seconds: its graphic is the swinging one
--   from a quarter of a second, with the sound of a hit, and nobody practices
--   on it until it stops. A bow or a crossbow cannot practice on it, and the
--   weapon must reach it: a tile, as the melee range of ultima.combat.max_range.
--   The skill is tried from -25 to 25 points: a player with none passes half of
--   the tries, and at 25 the dummy has nothing more to teach.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the dummy serial
-- ==============================================================================

training_dummy = {}

-- The client's own texts.
local RANGED = 501822      -- You can't practice ranged weapons on this.
local TOO_FAR = 501816     -- You are too far away to do that.
local SWINGING = 501815    -- You have to wait until it stops swinging.
local TOO_SKILLED = 501828 -- Your skill cannot improve any further by simply practicing with a dummy.

-- The skill is tried from MIN to MAX points, and a player at MAX has learned all it can.
local MIN = -25
local MAX = 25

-- When the dummy is hit and when it stops, in seconds after the swing.
local HIT_AFTER = 0.25
local STOPS_AFTER = 3

-- The sounds of a hit.
local SOUNDS = { 0x3A4, 0x3A6, 0x3A9, 0x3AE, 0x3B4, 0x3B6 }

-- The dummies that are swinging, by serial.
local swinging = {}

-- The graphic of the dummy at rest: the even one of the pair.
local function rest_graphic(serial)
    local graphic = item.item_id(serial)

    return graphic - graphic % 2
end

local function begin_swing(serial)
    local rest = rest_graphic(serial)

    swinging[serial] = true

    -- A restart of the server in the middle of a swing leaves the swinging graphic: the pair says how to go back.
    timer.after(HIT_AFTER, function()
        item.set_item_id(serial, rest + 1)
        item.play_sound(serial, SOUNDS[math.random(#SOUNDS)])
    end)

    -- The dummy may be gone by then: the graphic is the one it had, and a missing item takes nothing.
    timer.after(STOPS_AFTER, function()
        swinging[serial] = nil
        item.set_item_id(serial, rest)
    end)
end

-- Called when a player double clicks the dummy.
function training_dummy.on_use(serial, user)
    local weapon = combat.weapon(user)

    if weapon == nil then
        return true
    end

    if weapon.ranged then
        mobile.message_cliloc(user, RANGED)
    elseif not item.in_range(serial, user, weapon.range) then
        mobile.message_cliloc(user, TOO_FAR)
    elseif swinging[serial] then
        mobile.message_cliloc(user, SWINGING)
    elseif (mobile.skills(user)[weapon.skill] or 0) >= MAX then
        mobile.message_cliloc(user, TOO_SKILLED)
    else
        begin_swing(serial)

        local at = item.location(serial)

        combat.swing(user, at.x, at.y)
        skill.check(user, weapon.skill, MIN, MAX)
    end

    return true
end
