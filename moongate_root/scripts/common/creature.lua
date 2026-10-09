-- ==============================================================================
-- Moongate - scripts/common/creature.lua
--
-- What it is for:
--   What the mobile scripts of the creatures share: the monsters that go for the
--   players (mobiles/monster.lua), the animals that keep to themselves
--   (mobiles/animal.lua) and those that run from a blow (mobiles/scared_animal.lua).
--   A script takes it with local creature = require("common.creature") and makes
--   its own table with creature.new(options).
--
-- Options:
--   hunts   true: it goes for the players it sees and for the townsfolk, the NPCs with a
--           blue name (ModernUO's melee AI); false: it never starts a fight, it only
--           answers one. One that holds a bow or a crossbow is an archer: it stops where
--           the bow reaches and sees its prey, and shoots
--   flees   true: it does not fight back, it runs from who hit it
--   flee_at the percent of its hit points under which a creature that fights runs,
--           when its template sets none (flee_at): 20 for a monster, 10 for an animal.
--           At each think it then runs with a chance of one in ten, for 10 to 30
--           seconds, as ModernUO's; a template with flee_at -1 never does
--
-- Functions:
--   creature.new(options)  gives a table with on_think(serial) and on_speech(serial, speaker, text, keywords), the
--                          functions a mobile script defines
--
-- A creature a player tamed follows and obeys its owner (common/pet_orders.lua) and goes for nobody.
--
-- What a creature keeps:
--   Its state in memory by serial, not saved: after a restart, or once no player
--   is near, it starts again from wandering.
-- ==============================================================================

local pet_orders = require("common.pet_orders")

local creature = {}

-- How far a creature runs from who hit it, in cells, and for how many thinks (ten seconds).
local FLEE_DISTANCE = 12
local FLEE_THINKS = 20

-- A creature too hurt to fight (ModernUO's): at each think it may run with this chance, for ten to thirty seconds.
local HURT_FLEE_CHANCE = 0.1
local HURT_FLEE_MIN, HURT_FLEE_MAX = 20, 60

-- How many mobiles it looks at in a scan, nearest first: those that are no prey are passed over.
local SEEN = 6

-- How far a creature sees a player, and how far it follows one before it gives up (ModernUO: 16 and twice that).
local PERCEPTION = 16
local LEASH = 32

-- In thinks, two a second: how often it looks for a player, how long it stands guard, how long it tries a player it
-- cannot reach, and how long a rest lasts.
local SCAN_EVERY = 4
local GUARD_THINKS = 20
local GIVE_UP_THINKS = 40
local REST_MIN, REST_MAX = 30, 50

-- How far apart two heights of one storey are.
local STOREY = 16

-- A creature body's actions: it threatens and fidgets (ModernUO's choices); its attacks are the combat service's.
local THREATEN = MonsterAnimationType.Pillage
local FIDGETS = { MonsterAnimationType.Fidget1, MonsterAnimationType.Fidget2 }

-- Whether the body has the actions of a monster: a human or an animal body numbers its actions otherwise, and would
-- play a spell or a bow for these.
local function has_monster_actions(serial)
    return mobile.body_type(serial) == BodyType.Monster
end

-- What each creature is doing, by serial.
local minds = {}

local function mind_of(serial)
    local mind = minds[serial]

    if mind == nil then
        mind = { state = "wander", thinks = 0, rest = 0, until_think = 0, stalled = 0 }
        minds[serial] = mind

        -- A creature the script lost track of, as after a restart in the middle of a run, is not told to run still.
        if npc.get_prop(serial, "combat.passive") ~= nil then
            npc.set_prop(serial, "combat.passive", nil)
        end

        -- A creature the script lost track of, as after a script reload, may still be in war mode.
        local flags = mobile.flags(serial)

        if flags ~= nil and flags.war_mode then
            mobile.set_war_mode(serial, false)
        end
    end

    return mind
end

-- Whether the creature gave this player up as out of reach and the player still stands where it was then.
local function is_given_up(mind, player)
    local given_up = mind.given_up

    if given_up == nil or given_up.player ~= player then
        return false
    end

    local there = mobile.location(player)

    return there ~= nil and there.x == given_up.x and there.y == given_up.y
end

-- Whether the creature goes for the mobile: any player, and of the NPCs those with a blue name, the townsfolk. The yellow
-- ones, vendors, bankers and guards, cannot be hurt, and the others are its own kind, the animals or the people of
-- the wilds, left alone as ModernUO's creatures leave them.
local function is_prey(who)
    return mobile.is_player(who) or mobile.notoriety(who) == "innocent"
end

-- Whether a player tamed the creature: it has an owner, and goes for nobody.
local function is_owned(serial)
    local owner = npc.get_prop(serial, "owner")

    return owner ~= nil and owner ~= 0
end

-- The nearest player or NPC the creature goes for and sees, or nil. One it could not reach is left alone until it
-- moves, so the first few are asked for, and the first one that is prey and not given up is taken.
local function look_for_prey(serial, mind, hunts)
    if not hunts or is_owned(serial) then
        return nil
    end

    for _, who in ipairs(npc.mobiles_in_sight(serial, PERCEPTION, SEEN)) do
        if is_prey(who) and not is_given_up(mind, who) then
            return who
        end
    end

    return nil
end

local function start_chase(serial, mind, player)
    mind.state = "chase"
    mind.target = player
    mind.stalled = 0
    mind.rest = 0
    mind.given_up = nil
    mobile.set_war_mode(serial, true)
    npc.play_sound(serial, "start_attack")
    if has_monster_actions(serial) then
        mobile.animate(serial, THREATEN)
    end
end

-- Someone hit it, or missed it, and the combat service made it fight back: it turns on that one, whatever it was doing,
-- and without threatening it, since the fight has begun.
local function retaliate(serial, mind, attacker)
    mind.state = "chase"
    mind.target = attacker
    mind.stalled = 0
    mind.rest = 0
    mind.given_up = nil
    mobile.set_war_mode(serial, true)
end

local function start_guard(serial, mind)
    combat.stop(serial)
    mind.state = "guard"
    mind.target = nil
    mind.until_think = mind.thinks + GUARD_THINKS
end

local function start_wander(serial, mind)
    npc.set_prop(serial, "combat.passive", nil)
    combat.stop(serial)
    mind.state = "wander"
    mind.target = nil
    mobile.set_war_mode(serial, false)
end

-- Someone hit it and it runs: it stops fighting and walks away from the one who did, running, until it is far enough or
-- ten seconds have passed.
local function start_flee(serial, mind, attacker, thinks)
    combat.stop(serial)
    mind.state = "flee"
    mind.target = attacker
    mind.until_think = mind.thinks + (thinks or FLEE_THINKS)
    -- One that runs from a blow is far enough at some cells; one too hurt to fight runs for its whole time.
    mind.hurt = thinks ~= nil
    mobile.set_war_mode(serial, false)
    -- Told to the combat service, which makes every hit NPC answer the blow: this one only runs.
    npc.set_prop(serial, "combat.passive", true)
end

-- Whether the creature has lost enough of its hit points to run: under flee_at percent, the one of its template, else
-- the default of its script; never for flee_at -1.
local function too_hurt(serial, default)
    local at = npc.flee_at(serial) or default

    if at < 0 then
        return false
    end

    local stats = mobile.stats(serial)

    return stats ~= nil and stats.hits_max > 0 and stats.hits * 100 < stats.hits_max * at
end

local function flee(serial, mind, here)
    -- The combat service makes whoever is hit answer the blow: a creature that runs never does.
    combat.stop(serial)

    local from = mobile.location(mind.target)

    if from == nil
        or mind.thinks >= mind.until_think
        or (not mind.hurt and npc.distance_to(serial, from.x, from.y) >= FLEE_DISTANCE) then
        start_wander(serial, mind)

        return
    end

    -- The point on the far side of the creature from the one it runs from.
    local away_x = here.x + (here.x >= from.x and FLEE_DISTANCE or -FLEE_DISTANCE)
    local away_y = here.y + (here.y >= from.y and FLEE_DISTANCE or -FLEE_DISTANCE)

    -- The far corner; when the way is blocked, the same way along one axis, then along the other.
    if npc.walk_to(serial, away_x, away_y, here.z, 0, true) ~= "moving"
        and npc.walk_to(serial, away_x, here.y, here.z, 0, true) ~= "moving" then
        npc.walk_to(serial, here.x, away_y, here.z, 0, true)
    end
end

local function wander(serial, mind, hunts)
    if mind.thinks % SCAN_EVERY == 0 then
        local prey = look_for_prey(serial, mind, hunts)

        if prey ~= nil then
            start_chase(serial, mind, prey)

            return
        end
    end

    if mind.rest > 0 then
        mind.rest = mind.rest - 1

        return
    end

    -- One think in twenty it rests, with its idle sound and a fidget.
    if math.random(20) == 1 then
        mind.rest = math.random(REST_MIN, REST_MAX)
        npc.play_sound(serial, "idle")
        if has_monster_actions(serial) then
            mobile.animate(serial, FIDGETS[math.random(#FIDGETS)])
        end

        return
    end

    -- A step one think in four, never two thinks in a row: about one every two seconds.
    if mind.thinks % 2 == 0 and math.random(2) == 1 then
        npc.wander(serial)
    end
end

local function chase(serial, mind, here)
    local target = mind.target

    -- It follows what it saw without seeing it, up to the leash: not one that hid or left.
    if not npc.can_see(serial, target, LEASH, false) then
        start_guard(serial, mind)

        return
    end

    local there = mobile.location(target)
    local distance = npc.distance_to(serial, there.x, there.y)
    local level = math.abs(there.z - here.z) <= STOREY

    -- How far its blows reach: beside the prey for most, the range of its bow for an archer, which shoots what it sees.
    local range = combat.range(serial) or 1
    local reaches

    if range > 1 then
        reaches = distance <= range and level and npc.can_see(serial, target, range)
    else
        -- Beside it, and on its storey: one tile away on the floor above is not reached.
        reaches = distance <= 1 and level
    end

    if reaches then
        mind.stalled = 0
        npc.face(serial, there.x, there.y)

        -- In reach of its prey it fights it, once: the swings are the combat service's.
        if combat.target(serial) ~= target then
            combat.attack(serial, target)
        end

        return
    end

    -- An archer too far walks until the prey is a step inside its range; one in range that cannot see it comes closer.
    local stop = (range > 1 and distance > range) and (range - 1) or 1

    if npc.walk_to(serial, there.x, there.y, there.z, stop) == "moving" then
        mind.stalled = 0

        return
    end

    mind.stalled = mind.stalled + 1

    if mind.stalled >= GIVE_UP_THINKS then
        start_guard(serial, mind)
        mind.given_up = { player = target, x = there.x, y = there.y }
    end
end

-- The cell a step in each direction leads to: 0 north, 1 north-east, ... 7 north-west.
local dx = { [0] = 0, 1, 1, 1, 0, -1, -1, -1 }
local dy = { [0] = -1, -1, 0, 1, 1, 1, 0, -1 }

local function guard(serial, mind, here, hunts)
    if mind.thinks % 2 == 0 then
        local prey = look_for_prey(serial, mind, hunts)

        if prey ~= nil then
            start_chase(serial, mind, prey)

            return
        end
    end

    if mind.thinks >= mind.until_think then
        start_wander(serial, mind)

        return
    end

    -- One think in eight it looks another way.
    if math.random(8) == 1 then
        local direction = math.random(0, 7)
        npc.face(serial, here.x + dx[direction], here.y + dy[direction])
    end
end

-- Makes the table of a creature script, with its on_think.
function creature.new(options)
    local hunts = options.hunts == true
    local flees = options.flees == true
    local flee_at = options.flee_at or 20
    local script = {}

    -- Called on every think of an NPC near a player. It must not call wait().
    function script.on_think(serial)
        local here = npc.location(serial)

        if here == nil then
            minds[serial] = nil

            return
        end

        local mind = mind_of(serial)
        mind.thinks = mind.thinks + 1

        -- One that was tamed while it hunted lets go of what it hunted, unless it fights someone who hit it.
        if is_owned(serial) and mind.state ~= "wander" and combat.target(serial) == nil then
            start_wander(serial, mind)
        end

        -- Whoever it fights is the one it chases, even if it has not seen it: a creature that is hit does not go on
        -- strolling. One that runs does not fight: the combat service made it answer the blow, and it is told to stop.
        local fought = combat.target(serial)

        if fought ~= nil and (mind.state ~= "chase" or mind.target ~= fought) and (mind.state ~= "flee" or mind.target ~= fought) then
            if flees then
                start_flee(serial, mind, fought)
            else
                retaliate(serial, mind, fought)
            end
        end

        -- Fighting, and hurt: now and then it runs, instead of fighting to its death.
        if not flees and mind.state == "chase" and combat.target(serial) ~= nil and too_hurt(serial, flee_at)
            and math.random() < HURT_FLEE_CHANCE then
            start_flee(serial, mind, mind.target, math.random(HURT_FLEE_MIN, HURT_FLEE_MAX))
        end

        if mind.state == "flee" then
            flee(serial, mind, here)
        elseif mind.state == "chase" then
            chase(serial, mind, here)
        elseif is_owned(serial) then
            pet_orders.think(serial, mind, here)
        elseif mind.state == "guard" then
            guard(serial, mind, here, hunts)
        else
            wander(serial, mind, hunts)
        end
    end

    -- What its owner says to it: the words of a pet.
    function script.on_speech(serial, speaker, text, keywords)
        if is_owned(serial) then
            pet_orders.listen(serial, speaker, text, keywords)
        end
    end

    -- It dies, or is raised again: what it was doing is forgotten with it.
    function script.on_death(serial)
        npc.set_prop(serial, "combat.passive", nil)
        minds[serial] = nil
    end

    return script
end

return creature
