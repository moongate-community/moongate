-- ==============================================================================
-- Moongate - scripts/mobiles/healer.lua
--
-- What it is for:
--   The healers of the towns, as ModernUO's Healer and WanderingHealer: a ghost
--   that comes within 4 cells of one, with the healer in sight, is offered to
--   come back to life in the gump of the ankhs (templates/gumps/resurrect.xml).
--   The healer turns to the ghost, plays the sound 0x1F2 and the sparkles of
--   a resurrection on it. A healer makes one offer every 2 seconds at most, and
--   a ghost is offered only when it comes near: it must leave and come back to
--   be offered again. A mobile template uses it with script_id = "healer".
--
--   A criminal is refused (client text 501222). A player of negative karma is
--   told it has strayed (501224) and offered all the same. A healer of a
--   "whealer" template, a wandering healer, also strolls.
--
-- Functions:
--   on_think(serial)   every think of an NPC near a player
--                      (ultima.npcs.think_interval_ms, 500 ms); must not call
--                      wait()
--
-- What it keeps:
--   Who is near and when it last offered, in memory by serial, not saved.
-- ==============================================================================

healer = {}

-- How near a ghost must come, in cells.
local range = 4

-- Thinks between two offers of a healer: two seconds.
local between_offers = 4

-- A wandering healer takes a step every 4th think.
local stroll_every = 4

local offer_sound = 0x1F2

-- Client texts: "Thou art a criminal. I shall not resurrect thee.", "Thou hast strayed
-- from the path of virtue, but thou still deservest a second chance."
local criminal_cliloc = 501222
local strayed_cliloc = 501224

local thinks = {}
local last_offer = {}
local near = {}

-- Whether the healer goes for walks: the wandering ones, whose template ends with "whealer".
local function wanders(serial)
    local template = mobile.template(serial)

    return template ~= nil and template:sub(-7) == "whealer"
end

-- Offers the ghost to come back. False when the healer refuses it.
local function offer(serial, ghost)
    if mobile.criminal(ghost) then
        npc.say_cliloc(serial, criminal_cliloc)

        return
    end

    local stats = mobile.stats(ghost)

    if stats and stats.karma < 0 then
        npc.say_cliloc(serial, strayed_cliloc)
    end

    npc.look_at(serial, ghost)
    npc.play_sound(serial, offer_sound)
    effect.on(ghost, EffectGraphicType.SparkleHeal)
    gump.open(ghost, "resurrect", { healer = serial })
end

function healer.on_think(serial)
    local count = (thinks[serial] or 0) + 1
    thinks[serial] = count

    local before = near[serial] or {}
    local now = {}

    for _, ghost in ipairs(npc.ghosts_in_sight(serial, range)) do
        if before[ghost] then
            now[ghost] = true
        elseif count - (last_offer[serial] or -between_offers) >= between_offers then
            -- A ghost met while the healer waits for its turn is offered when the wait is over.
            last_offer[serial] = count
            now[ghost] = true
            offer(serial, ghost)
        end
    end

    near[serial] = now

    if count % stroll_every == 0 and wanders(serial) then
        npc.wander(serial)
    end
end
