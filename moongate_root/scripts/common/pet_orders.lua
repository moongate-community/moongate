-- ==============================================================================
-- Moongate - scripts/common/pet_orders.lua
--
-- What it is for:
--   What the creatures that belong to a player do, and what their owner says to them. The order of a pet is the prop
--   pet.order of the creature: follow (also when it has none), stay, come or guard. Used by common/creature.lua.
--
--   follow  the pet stays near its owner: it walks when it is farther than 2 tiles and runs from 7, and when it
--           cannot get there in 10 steps it is moved beside the owner. Beyond 24 tiles, on another map, or when its owner
--           is not in the world, it stands still.
--   come    as follow, and when it is beside the owner it stays.
--   stay    it stands still.
--   guard   it stays within 3 tiles of its owner and fights whoever fights the owner or the pet.
--
--   The words, said by the owner within 14 tiles: come, follow, follow me, stay, stop, guard, kill, attack and release,
--   with "all" before them for every pet within reach (one pet answers for them all), or with the name of the pet first
--   for that pet. Kill and attack ask for a target; release asks for a yes.
--
-- Functions:
--   think(serial, mind, here)                 one think of an owned pet that is not fighting
--   listen(serial, speaker, text, keywords)   what a pet hears
-- ==============================================================================

local pet_orders = {}

-- Farther than this from its owner a pet walks, from RUN_FROM on it runs, beyond FOLLOW_LIMIT it stays.
local FOLLOW_CLOSE = 2
local RUN_FROM = 7
local FOLLOW_LIMIT = 24

-- Failed steps in a row after which a pet is moved beside its owner.
local STUCK_STEPS = 10

-- How near a guard stays to its owner, how far it sees an enemy, how many it looks at, and how often.
local GUARD_CLOSE = 3
local GUARD_SIGHT = 16
local GUARD_SEEN = 6
local GUARD_EVERY = 2

-- How far the owner may be to be heard, in tiles.
local HEARING = 14

local ORDER = "pet.order"

-- The words of the client by name, one pet, and "all", every pet.
local NAMED = {
    [SpeechKeywordType.PetCome] = "come",
    [SpeechKeywordType.PetFollow] = "follow",
    [SpeechKeywordType.PetFollowMe] = "follow",
    [SpeechKeywordType.PetStay] = "stay",
    [SpeechKeywordType.PetStop] = "stop",
    [SpeechKeywordType.PetGuard] = "guard",
    [SpeechKeywordType.PetKill] = "kill",
    [SpeechKeywordType.PetAttack] = "kill",
    [SpeechKeywordType.PetRelease] = "release",
}

local ALL = {
    [SpeechKeywordType.AllCome] = "come",
    [SpeechKeywordType.AllFollow] = "follow",
    [SpeechKeywordType.AllFollowMe] = "follow",
    [SpeechKeywordType.AllStay] = "stay",
    [SpeechKeywordType.AllStop] = "stop",
    [SpeechKeywordType.AllGuard] = "guard",
    [SpeechKeywordType.AllGuardMe] = "guard",
    [SpeechKeywordType.AllKill] = "kill",
    [SpeechKeywordType.AllAttack] = "kill",
}

local function owner_of(serial)
    local owner = npc.get_prop(serial, "owner")

    if owner == nil or owner == 0 then
        return nil
    end

    return owner
end

local function order_of(serial)
    return npc.get_prop(serial, ORDER) or "follow"
end

local function distance(a, b)
    if a == nil or b == nil or a.map ~= b.map then
        return math.huge
    end

    return math.max(math.abs(a.x - b.x), math.abs(a.y - b.y))
end

-- Walks the pet to its owner, or moves it beside when it cannot get there.
local function follow(serial, mind, here, there, close)
    local far = distance(here, there)

    if far > FOLLOW_LIMIT then
        return
    end

    if far <= close then
        mind.stuck = 0

        return
    end

    local result = npc.walk_to(serial, there.x, there.y, there.z, 1, far >= RUN_FROM)

    if result == "blocked" or result == "no_path" then
        mind.stuck = (mind.stuck or 0) + 1

        if mind.stuck >= STUCK_STEPS then
            mobile.teleport(serial, there.x, there.y, there.z, there.map)
            mind.stuck = 0
        end
    else
        mind.stuck = 0
    end
end

-- Whoever in sight fights the owner or the pet, and is not one of the owner's own.
local function find_enemy(serial, owner)
    for _, who in ipairs(npc.mobiles_in_sight(serial, GUARD_SIGHT, GUARD_SEEN)) do
        if who ~= owner and who ~= serial and owner_of(who) ~= owner and not mobile.is_dead(who) then
            local target = combat.target(who)

            if target == owner or target == serial then
                return who
            end
        end
    end

    return nil
end

function pet_orders.think(serial, mind, here)
    local owner = owner_of(serial)
    local there = owner and mobile.location(owner)

    if there == nil or there.map ~= here.map then
        return
    end

    local order = order_of(serial)

    if order == "stay" then
        return
    end

    if order == "guard" then
        if mind.thinks % GUARD_EVERY == 0 then
            local enemy = find_enemy(serial, owner)

            if enemy ~= nil then
                combat.attack(serial, enemy)

                return
            end
        end

        follow(serial, mind, here, there, GUARD_CLOSE)

        return
    end

    if order == "come" and distance(here, there) <= FOLLOW_CLOSE then
        npc.set_prop(serial, ORDER, "stay")

        return
    end

    follow(serial, mind, here, there, FOLLOW_CLOSE)
end

local function command_of(keywords, words)
    for _, keyword in ipairs(keywords or {}) do
        if words[keyword] then
            return words[keyword]
        end
    end

    return nil
end

local function starts_with(text, name)
    return name ~= nil and #name > 0 and text:lower():sub(1, #name) == name:lower()
end

-- The pets of the speaker within reach of the one that heard: it, and those near it.
local function pets_near(serial, speaker)
    local pets = { serial }

    for _, other in ipairs(npc.nearby(serial, HEARING, "npcs")) do
        if owner_of(other) == speaker then
            pets[#pets + 1] = other
        end
    end

    return pets
end

-- What a pet fights because its owner said so: not the owner, not one of its own, not itself.
local function attack(pet, speaker, target)
    if target ~= speaker and target ~= pet and owner_of(target) ~= speaker and not mobile.is_dead(target) then
        combat.attack(pet, target)
    end
end

local function carry_out(command, speaker, pets)
    if command == "kill" then
        target.pick(speaker, function(picked)
            if picked.kind ~= "object" then
                return
            end

            for _, pet in ipairs(pets) do
                attack(pet, speaker, picked.serial)
                npc.play_sound(pet, "attack")
            end
        end)

        return
    end

    if command == "release" then
        gump.open(speaker, "pet_release", { pet = pets[1] })

        return
    end

    for _, pet in ipairs(pets) do
        if command == "stop" then
            npc.set_prop(pet, ORDER, "stay")
            combat.stop(pet)
        else
            npc.set_prop(pet, ORDER, command)
        end

        npc.play_sound(pet, "idle")
    end
end

function pet_orders.listen(serial, speaker, text, keywords)
    if owner_of(serial) ~= speaker or mobile.is_dead(speaker) then
        return
    end

    if distance(npc.location(serial), mobile.location(speaker)) > HEARING then
        return
    end

    local all = command_of(keywords, ALL)

    if all ~= nil then
        -- Every pet hears the words: one of them answers for all.
        if pet.attend(speaker) then
            carry_out(all, speaker, pets_near(serial, speaker))
        end

        return
    end

    local named = command_of(keywords, NAMED)

    if named ~= nil and starts_with(text, npc.name(serial)) then
        carry_out(named, speaker, { serial })
    end
end

return pet_orders
