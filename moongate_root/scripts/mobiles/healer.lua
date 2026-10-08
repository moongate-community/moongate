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
--   A criminal is refused (client text 501222), and so is a murderer (501223). A
--   player of negative karma is told it has strayed (501224) and offered all the
--   same. An evil healer, of a template whose id starts with "evil", refuses
--   nobody: it raises the red players too. A healer of a "whealer" template, a
--   wandering healer, also strolls.
--
-- Functions:
--   on_think(serial)   every think of an NPC near a player
--                      (ultima.npcs.think_interval_ms, 500 ms); must not call
--                      wait()
--   on_speech, on_context_menu, on_context_menu_select, on_drag_drop
--                      the lessons of common/training.lua: a healer teaches the
--                      skills it has at 60.0 or more for gold
--
-- What it keeps:
--   Who is near and when it last offered, in memory by serial, not saved.
-- ==============================================================================

local training = require("common.training")

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
local murderer_cliloc = 501223
local strayed_cliloc = 501224

local thinks = {}
local last_offer = {}
local near = {}

-- Whether the healer goes for walks: the wandering ones, whose template ends with "whealer".
local function wanders(serial)
    local template = mobile.template(serial)

    return template ~= nil and template:sub(-7) == "whealer"
end

-- Whether the healer is an evil one, who turns nobody away.
local function is_evil(serial)
    local template = mobile.template(serial)

    return template ~= nil and template:sub(1, 4) == "evil"
end

-- Offers the ghost to come back. False when there was nobody to offer to, such as a ghost with no
-- client left; a refusal is an answer, so it is true.
local function offer(serial, ghost)
    local evil = is_evil(serial)

    if not evil and mobile.criminal(ghost) then
        npc.say_cliloc(serial, criminal_cliloc)

        return true
    end

    if not evil and mobile.is_murderer(ghost) then
        npc.say_cliloc(serial, murderer_cliloc)

        return true
    end

    local stats = mobile.stats(ghost)

    if not evil and stats and stats.karma < 0 then
        npc.say_cliloc(serial, strayed_cliloc)
    end

    npc.look_at(serial, ghost)
    npc.play_sound(serial, offer_sound)
    effect.on(ghost, EffectGraphicType.SparkleHeal)

    return gump.open(ghost, "resurrect", { healer = serial })
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
            if offer(serial, ghost) then
                last_offer[serial] = count
                now[ghost] = true
            end
        end
    end

    near[serial] = now

    if count % stroll_every == 0 and wanders(serial) then
        npc.wander(serial)
    end
end

-- A healer teaches the skills it has, as ModernUO's do (common/training.lua).
local menu_range = 8

function healer.on_speech(serial, speaker, text, keywords)
    training.listen(serial, speaker, keywords)
end

function healer.on_context_menu(serial, player)
    return training.entries(serial, player, menu_range)
end

function healer.on_context_menu_select(serial, player, id)
    training.select(serial, player, id)
end

function healer.on_drag_drop(serial, giver, item)
    return training.drop(serial, giver, item)
end
