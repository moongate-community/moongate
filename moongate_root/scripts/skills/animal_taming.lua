-- ==============================================================================
-- Moongate - scripts/skills/animal_taming.lua
--
-- What it is for:
--   The Animal Taming skill. The player uses the skill, picks a creature and, if it can be tamed
--   (data/taming.toml), stays within reach while it says kind things to it: three or four times, three seconds
--   apart. At each time the checks are made again; at the last the skill is rolled, from 0.1 under the Animal Taming the
--   creature asks to 49.9 above it, and a success makes the creature the player's own (the pet module), so it can be
--   ridden, stabled and so on. A failure costs nothing but the time.
--
-- The checks, at the pick (the client's own texts): the target is a creature, not a player; it can be tamed; it has no
--   owner; its followers fit in the player's; the player has the skill it asks; it is within 3 tiles; nobody else is
--   taming it. At each time: within 7 tiles, the player alive, in line of sight, the creature still wild and not hurt
--   since the start.
--
-- Functions:
--   on_use(user)   the skill was used; returns the seconds before the next skill, 1
--   roll()         math.random; a test replaces it
-- ==============================================================================

animal_taming = {}

animal_taming.roll = math.random

-- Seconds between two times, and how many there are: three, or four.
local TICK = 3
local TICKS = 3
local EXTRA_TICK_CHANCE = 0.5

-- How far the creature may be at the pick and during the taming, in tiles.
local PICK_RANGE = 3
local TAMING_RANGE = 7

-- The window of the roll around the skill the creature asks, in points.
local BELOW = 0.1
local ABOVE = 49.9

-- Seconds before the next use of a skill when it is used again at once: the cursor is the wait.
local DELAY = 1

-- The client's texts.
local WHICH = 502789          -- Tame which animal?
local NOT_A_CREATURE = 502801 -- You can't tame that!
local NOT_A_ANIMAL = 502469   -- That being cannot be tamed.
local NOT_TAMABLE = 1049655   -- That creature cannot be tamed.
local ALREADY_TAMED = 502804  -- That creature looks tame already.
local TOO_MANY = 1049611      -- You have too many followers to tame that creature.
local NO_CHANCE = 502806      -- You have no chance of taming this creature.
local TOO_FAR = 500446        -- That is too far away.
local BUSY = 502802           -- Someone else is already taming this creature.
local START = 1010597         -- You start to tame the creature.
local WANDERED = 502795       -- You are too far away to continue taming.
local DEAD = 502796           -- You are dead and cannot continue taming.
local NO_SIGHT = 1049654      -- You can no longer see the creature.
local HURT = 502794           -- The animal is too angry to continue taming.
local ACCEPT = 502799         -- It seems to accept you as master.
local FAILED = 502798         -- You fail to tame the creature.

-- What the tamer says while it works, three groups of kind words of the client; one group is picked, then a line of it.
local LINES = {
    { 502790, 502791, 502792, 502793 },
    { 1005608, 1005609, 1005610, 1005611, 1005612, 1005613 },
    { 1010593, 1010594, 1010595, 1010596 },
}

-- Who is taming (a player), and who is being tamed (a creature): one at a time each.
local taming = {}
local being_tamed = {}

local function distance(a, b)
    if a == nil or b == nil or a.map ~= b.map then
        return math.huge
    end

    return math.max(math.abs(a.x - b.x), math.abs(a.y - b.y))
end

local function finish(user, creature)
    taming[user] = nil
    being_tamed[creature] = nil
end

local function pick_one(list)
    return list[math.min(math.floor(animal_taming.roll() * #list) + 1, #list)]
end

-- Why the pick is refused: the cliloc, or nil when it is fine.
local function refusal(user, creature)
    local there = mobile.location(creature)

    if there == nil then
        return NOT_A_CREATURE
    end

    if mobile.is_player(creature) then
        return NOT_A_ANIMAL
    end

    local info = pet.info(creature)

    if info == nil then
        return NOT_TAMABLE
    end

    if info.owner ~= 0 then
        return ALREADY_TAMED
    end

    if pet.followers(user) + info.slots > pet.max_followers() then
        return TOO_MANY
    end

    if (mobile.skills(user).animal_taming or 0) < info.min_skill then
        return NO_CHANCE
    end

    if distance(mobile.location(user), there) > PICK_RANGE then
        return TOO_FAR
    end

    if being_tamed[creature] ~= nil then
        return BUSY
    end

    return nil
end

-- Why the taming stops at a time: the cliloc, or nil when it goes on.
local function interruption(user, creature, hits)
    local here, there = mobile.location(user), mobile.location(creature)

    if there == nil or distance(here, there) > TAMING_RANGE then
        return WANDERED
    end

    if mobile.is_dead(user) then
        return DEAD
    end

    if not world.line_of_sight(here.map, here.x, here.y, here.z, there.x, there.y, there.z) then
        return NO_SIGHT
    end

    local info = pet.info(creature)

    if info == nil or info.owner ~= 0 then
        return ALREADY_TAMED
    end

    if mobile.stats(creature).hits < hits then
        return HURT
    end

    return nil
end

-- What pet.tame answers when it refuses, as the client says it.
local TAME_REFUSALS = {
    [PetResultType.NotAnNpc] = NOT_A_CREATURE,
    [PetResultType.NotTamable] = NOT_TAMABLE,
    [PetResultType.AlreadyOwned] = ALREADY_TAMED,
    [PetResultType.TooManyFollowers] = TOO_MANY,
}

local function roll(user, creature)
    local min = pet.info(creature).min_skill

    if not skill.check(user, "animal_taming", min - BELOW, min + ABOVE) then
        mobile.message_cliloc(user, FAILED)

        return
    end

    local result = pet.tame(user, creature)

    if result == PetResultType.Ok then
        mobile.message_cliloc(user, ACCEPT)
    elseif TAME_REFUSALS[result] then
        mobile.message_cliloc(user, TAME_REFUSALS[result])
    end
end

local function step(user, creature, hits, time, times)
    local stop = interruption(user, creature, hits)

    if stop ~= nil then
        mobile.message_cliloc(user, stop)
        finish(user, creature)

        return
    end

    if time < times then
        mobile.message_cliloc(user, pick_one(pick_one(LINES)))
        timer.after(TICK, function()
            step(user, creature, hits, time + 1, times)
        end)

        return
    end

    finish(user, creature)
    roll(user, creature)
end

local function begin(user, creature)
    local why = refusal(user, creature)

    if why ~= nil then
        mobile.message_cliloc(user, why)

        return
    end

    taming[user] = true
    being_tamed[creature] = user
    mobile.message_cliloc(user, START)

    local times = TICKS + (animal_taming.roll() < EXTRA_TICK_CHANCE and 0 or 1)
    local hits = mobile.stats(creature).hits

    timer.after(TICK, function()
        step(user, creature, hits, 1, times)
    end)
end

function animal_taming.on_use(user)
    if taming[user] then
        return DELAY
    end

    mobile.message_cliloc(user, WHICH)

    target.pick(user, function(picked)
        if picked.kind ~= "object" or taming[user] then
            return
        end

        begin(user, picked.serial)
    end)

    return DELAY
end
