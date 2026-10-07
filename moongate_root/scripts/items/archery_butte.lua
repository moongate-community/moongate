-- ==============================================================================
-- Moongate - scripts/items/archery_butte.lua
--
-- What it is for:
--   The item script of the archery buttes (0x100A facing east, 0x100B facing
--   south), as ModernUO's ArcheryButte. A template uses it with
--   script_id = "archery_butte" and use_range = 6, so the player may shoot from
--   the distance the butte asks.
--
--   Shooting: a player with a bow or a crossbow stands in front of the butte, in
--   line with it, five or six tiles away, and double clicks it. An arrow or a
--   bolt is spent, the player shoots and the archery of the weapon is tried from
--   -25 to 25 points: it may rise, and the shot may miss. A shot that hits scores
--   50 (the bullseye, one in ten), 10, 5 or 2 points, and tells the total of the
--   player at the butte so far. The arrow may split, which is more likely the more
--   ammunition is stuck in the butte. The player is told, and nobody else.
--   Gathering: double clicking the butte within a tile, when arrows or bolts are
--   stuck in it, gives them back to the backpack and clears the scores.
--
--   Two seconds go between two shots at the butte.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the butte serial
-- ==============================================================================

archery_butte = {}

-- The client's own texts.
local GATHERED = 500592         -- You gather the arrows and bolts.
local NOT_RANGED = 500593       -- You must practice with ranged weapons on this.
local NO_ARROWS = 500594        -- You do not have any arrows with which to practice.
local NO_BOLTS = 500595         -- You do not have any crossbow bolts with which to practice.
local STAND_IN_FRONT = 500596   -- You would do better to stand in front of the archery butte.
local NOT_LINED_UP = 500597     -- You aren't properly lined up with the archery butte to get an accurate shot.
local TOO_FAR = 500598          -- You are too far away from the archery butte to get an accurate shot.
local TOO_CLOSE = 500599        -- You are too close to the target.
local MISSED = 500604           -- You miss the target altogether.
local SPLIT_FIRST = 1010027     -- {0} splits an {1} in the bullseye... (four texts, by area)
local HIT_FIRST = 1010035       -- {0} hits the target in the bullseye... (four texts, by area)
local TOTAL_ONE = 1062719       -- Total: {0}
local TOTAL_MORE = 1042683      -- Total: {0} in {1} shots

local FACING_EAST = 0x100A
local ARROW = "0x0f3f_arrow"
local BOLT = "0x1bfb_crossbow_bolt"

-- The skill is tried from MIN to MAX points, as the dummy's.
local MIN = -25
local MAX = 25

-- How far the player may stand, and how near it may not.
local FARTHEST = 6
local NEAREST = 5
local PICK_UP_RANGE = 1

-- Seconds between two shots at a butte.
local DELAY = 2

-- The sound of an arrow in the butte, and the speed of the flying arrow.
local HIT_SOUND = 0x2B1
local ARROW_SPEED = 18

-- What a shot scores by area: bullseye, inner, middle and outer ring, the chance of each (cumulative) and the score
-- of an arrow that splits.
local AREAS = {
    { upto = 0.10, score = 50, split = 100 },
    { upto = 0.25, score = 10, split = 20 },
    { upto = 0.50, score = 5, split = 15 },
    { upto = 1.01, score = 2, split = 5 },
}

-- When each butte may be shot again, and what each player scored at it.
local last_use = {}
local scores = {}

local function told(serial, user, cliloc, args)
    item.message_cliloc(serial, user, cliloc, args)
end

local function gather(serial, user)
    local arrows = item.get_prop(serial, "butte.arrows") or 0
    local bolts = item.get_prop(serial, "butte.bolts") or 0

    mobile.message_cliloc(user, GATHERED)

    if arrows > 0 then
        item.give(user, ARROW, arrows)
    end

    if bolts > 0 then
        item.give(user, BOLT, bolts)
    end

    item.set_prop(serial, "butte.arrows")
    item.set_prop(serial, "butte.bolts")
    scores[serial] = nil
end

local function record(serial, user, points)
    scores[serial] = scores[serial] or {}

    local entry = scores[serial][user] or { total = 0, count = 0 }

    entry.total = entry.total + points
    entry.count = entry.count + 1
    scores[serial][user] = entry

    if entry.count == 1 then
        told(serial, user, TOTAL_ONE, tostring(entry.total))
    else
        told(serial, user, TOTAL_MORE, entry.total .. "\t" .. entry.count)
    end
end

-- Why the player cannot shoot from where it stands, or nil: a text of the client.
local function why_not(serial, user)
    local butte = item.location(serial)
    local here = mobile.location(user)
    local east = item.item_id(serial) == FACING_EAST

    if (east and here.x <= butte.x) or (not east and here.y <= butte.y) then
        return STAND_IN_FRONT
    end

    if (east and here.y ~= butte.y) or (not east and here.x ~= butte.x) then
        return NOT_LINED_UP
    end

    local distance = math.max(math.abs(here.x - butte.x), math.abs(here.y - butte.y))

    if distance > FARTHEST then
        return TOO_FAR
    end

    if distance < NEAREST then
        return TOO_CLOSE
    end

    return nil
end

local function fire(serial, user)
    local weapon = combat.weapon(user)

    if weapon == nil or not weapon.ranged then
        mobile.message_cliloc(user, NOT_RANGED)

        return
    end

    local now = world.now()

    if last_use[serial] ~= nil and now < last_use[serial] + DELAY then
        return
    end

    local problem = why_not(serial, user)

    if problem ~= nil then
        mobile.message_cliloc(user, problem)

        return
    end

    if not combat.spend_ammo(user) then
        mobile.message_cliloc(user, weapon.ammo == "arrow" and NO_ARROWS or NO_BOLTS)

        return
    end

    last_use[serial] = now

    local butte = item.location(serial)
    local name = mobile.name(user) or ""

    combat.swing(user, butte.x, butte.y)
    effect.moving(user, serial, weapon.projectile, { speed = ARROW_SPEED })

    if not skill.check(user, weapon.skill, MIN, MAX) then
        told(serial, user, MISSED, name)
        record(serial, user, 0)

        return
    end

    item.play_sound(serial, HIT_SOUND)

    local roll = math.random()
    local area = 1

    while roll >= AREAS[area].upto do
        area = area + 1
    end

    local stuck_arrows = item.get_prop(serial, "butte.arrows") or 0
    local stuck_bolts = item.get_prop(serial, "butte.bolts") or 0

    -- The more ammunition is in the butte, the likelier an arrow splits one of them.
    local split = (stuck_arrows + stuck_bolts) * 0.02 > math.random()

    if split then
        told(serial, user, SPLIT_FIRST + area - 1, name .. "\t" .. weapon.ammo)
        record(serial, user, AREAS[area].split)

        return
    end

    told(serial, user, HIT_FIRST + area - 1, name)

    if weapon.ammo == "arrow" then
        item.set_prop(serial, "butte.arrows", stuck_arrows + 1)
    else
        item.set_prop(serial, "butte.bolts", stuck_bolts + 1)
    end

    record(serial, user, AREAS[area].score)
end

-- Called when a player double clicks the butte.
function archery_butte.on_use(serial, user)
    local stuck = (item.get_prop(serial, "butte.arrows") or 0) + (item.get_prop(serial, "butte.bolts") or 0)

    if stuck > 0 and item.in_range(serial, user, PICK_UP_RANGE) then
        gather(serial, user)
    else
        fire(serial, user)
    end

    return true
end
