-- ==============================================================================
-- Moongate - scripts/skills/detecting_hidden.lua
--
-- What it is for:
--   The skill script of Detecting Hidden, as ModernUO's: the player picks a place
--   within 12 tiles, or itself, and looks for the hidden around it, up to a tenth
--   of its skill in tiles (half of that when the skill check from 0 to 100
--   fails). Each hidden player or NPC there is found when the detector's skill
--   plus a roll of -10 to 10 is not under the hider's Hiding plus its own roll;
--   a found one is shown and told "You have been revealed!". Nothing found reads
--   "You can see nothing hidden there." Staff are never found by lesser ones. It
--   waits the 30 seconds of data/skills.toml.
--
--   Not there yet: traps, houses and the factions.
--
-- Functions:
--   on_use(user)   the player user uses the skill
-- ==============================================================================

detecting_hidden = {}

-- The client's own texts.
local WHERE = 500819       -- Where will you search?
local REVEALED = 500814    -- You have been revealed!
local NOTHING = 500817     -- You can see nothing hidden there.

local SIGHT = 12

-- The skill's own roll, from -10 to 10 on each side.
local ROLL = 10

function detecting_hidden.on_use(user)
    mobile.message_cliloc(user, WHERE)

    target.pick_location(user, function(picked)
        local here = mobile.location(user)

        if picked.kind == "canceled" or here == nil then
            return
        end

        local place = here

        if picked.kind == "location" then
            place = picked
        elseif picked.serial ~= user then
            mobile.message_cliloc(user, NOTHING)

            return
        end

        if math.max(math.abs(place.x - here.x), math.abs(place.y - here.y)) > SIGHT then
            mobile.message_cliloc(user, NOTHING)

            return
        end

        local points = mobile.skills(user).detecting_hidden or 0
        local range = math.floor(points / 10)

        if not skill.check(user, "detecting_hidden", 0, 100) then
            range = math.floor(range / 2)
        end

        local found = false

        for _, who in ipairs(world.mobiles_in_range(here.map, place.x, place.y, range)) do
            local flags = mobile.flags(who)

            if who ~= user and flags ~= nil and flags.hidden and not world.is_staff(who) then
                local hider = (mobile.skills(who).hiding or 0) + math.random(-ROLL, ROLL)
                local detector = points + math.random(-ROLL, ROLL)

                if detector >= hider then
                    mobile.set_hidden(who, false)
                    mobile.message_cliloc(who, REVEALED)
                    found = true
                end
            end
        end

        if not found then
            mobile.message_cliloc(user, NOTHING)
        end
    end)
end
