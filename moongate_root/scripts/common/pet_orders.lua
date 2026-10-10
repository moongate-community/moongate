-- ==============================================================================
-- Moongate - scripts/common/pet_orders.lua
--
-- What it is for:
--   What the creatures that belong to a player do, and what their owner says to them. The order of a pet is the prop
--   pet.order of the creature: follow (also when it has none), stay, come or guard. Used by common/creature.lua.
--
--   follow  the pet stays near its owner: it walks when it is farther than 2 tiles and runs from 7, taking three steps a
--           think from then on to keep up; when it cannot get there in 10 steps, or falls more than 16 tiles behind, it is
--           moved to a free tile beside the owner, one a walking mobile could step onto (world.spot_beside), so not
--           behind a wall; if there is none it stays. Beyond 24 tiles, on another map, or when its owner is not in the
--           world, it stands still.
--   come    as follow, and when it is beside the owner it stays.
--   stay    it stands still.
--   guard   it stays within 3 tiles of its owner and fights whoever fights the owner or the pet.
--
--   Every order but release can be refused: the pet obeys with the chance pet.control_chance gives (its owner's Animal
--   Taming and Animal Lore against the skill it asks, and its loyalty). One that obeys gains a point of loyalty; one that
--   does not shows its anger, loses three, and goes wild when it has none left.
--
--   The words, said by the owner within 14 tiles: come, follow, follow me, stay, stop, guard, kill, attack and release,
--   with "all" before them for every pet that hears, or with the name of the pet first for that pet. Kill and attack ask
--   for a target (one pet asks for all, and the owner is a criminal when it sends them against an innocent); release asks
--   for a yes. A pet that flees by nature fights when it is told to.
--
-- Functions:
--   think(serial, mind, here)                 one think of an owned pet that is not fighting
--   listen(serial, speaker, text, keywords)   what a pet hears
--   feed(serial, giver, given)                food dropped on a pet: its owner's gives it loyalty, and bonds it in time; true when eaten
-- ==============================================================================

local pet_orders = {}

-- Farther than this from its owner a pet walks, from RUN_FROM on it runs, beyond FOLLOW_LIMIT it stays.
local FOLLOW_CLOSE = 2
local RUN_FROM = 7
local FOLLOW_LIMIT = 24

-- Failed steps in a row after which a pet is moved beside its owner, how far behind it is moved, and how many steps a
-- running pet takes in a think (the owner moves faster than one step in half a second).
local STUCK_STEPS = 10
local LAGGING = 16
local RUN_STEPS = 3

-- How near a guard stays to its owner, how far it sees an enemy, how many it looks at, and how often.
local GUARD_CLOSE = 3
local GUARD_SIGHT = 16
local GUARD_SEEN = 16
local GUARD_EVERY = 2

-- How far the owner may be to be heard, in tiles.
local HEARING = 14

local ORDER = "pet.order"

-- The pet eats and shows it, a happier pet says so, one that does not like the food shies away.
local EAT = MonsterAnimationType.Fidget1
local HAPPIER = 502060
local SHIES_AWAY = 1043257
local WILD = 1043255
local BONDED = 1049666 -- Your pet has bonded with you!

-- What a pet that does not obey does: it growls and fidgets, angry.
local ANGER = MonsterAnimationType.Fidget2

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

-- Moves the pet to a free tile beside its owner, when there is one.
local function rescue(serial, mind, there)
    local spot = world.spot_beside(there.map, there.x, there.y, there.z)

    if spot ~= nil then
        mobile.teleport(serial, spot.x, spot.y, spot.z, spot.map)
    end

    mind.stuck = 0
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

    -- Far behind: it is where its owner is.
    if far > LAGGING then
        rescue(serial, mind, there)

        return
    end

    local result

    for _ = 1, far >= RUN_FROM and RUN_STEPS or 1 do
        result = npc.walk_to(serial, there.x, there.y, there.z, 1, far >= RUN_FROM)

        if result ~= "moving" then
            break
        end
    end

    if result == "blocked" or result == "no_path" then
        mind.stuck = (mind.stuck or 0) + 1

        if mind.stuck >= STUCK_STEPS then
            rescue(serial, mind, there)
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

-- The pets of the speaker within hearing of it.
local function pets_near(speaker)
    local pets = {}
    local there = mobile.location(speaker)

    for _, other in ipairs(world.mobiles_in_range(there.map, there.x, there.y, HEARING)) do
        if owner_of(other) == speaker then
            pets[#pets + 1] = other
        end
    end

    return pets
end

-- Whether sending a pet against the target is a crime of the owner: the target is innocent and has not gone for the owner.
local function is_crime(speaker, target)
    return (mobile.is_player(target) or mobile.notoriety(target) == "innocent") and combat.target(target) ~= speaker
end

-- Whether a pet may be sent against the target: not the owner, not one of its own, not itself, not dead.
local function can_attack(pet, speaker, target)
    return target ~= speaker and target ~= pet and owner_of(target) ~= speaker and not mobile.is_dead(target)
end

-- Whether a pet obeys its owner now: the roll of its loyalty. One that does not shows it.
local function obeys(pet_serial, speaker)
    local result = pet.obey(speaker, pet_serial)

    if result == PetObeyResultType.Obeyed then
        return true
    end

    if result == PetObeyResultType.Disobeyed or result == PetObeyResultType.Wild then
        npc.play_sound(pet_serial, "attack")
        mobile.animate(pet_serial, ANGER)
    end

    if result == PetObeyResultType.Wild then
        npc.say_cliloc(pet_serial, WILD)
    end

    return false
end

local function carry_out(command, speaker, pets)
    if command == "kill" then
        target.pick(speaker, function(picked)
            if picked.kind ~= "object" then
                return
            end

            -- The owner sends them: a crime against an innocent, as if it had struck it.
            if picked.serial ~= speaker and is_crime(speaker, picked.serial) then
                mobile.set_criminal(speaker, true)
            end

            for _, pet in ipairs(pets) do
                -- A pet let go, or dead, since the words, does not obey them.
                if owner_of(pet) == speaker and can_attack(pet, speaker, picked.serial) and obeys(pet, speaker)
                    and combat.attack(pet, picked.serial) then
                    npc.play_sound(pet, "attack")
                end
            end
        end)

        return
    end

    if command == "release" then
        gump.open(speaker, "pet_release", { pet = pets[1] })

        return
    end

    for _, pet in ipairs(pets) do
        if obeys(pet, speaker) then
            if command == "stop" then
                npc.set_prop(pet, ORDER, "stay")
                combat.stop(pet)
            else
                npc.set_prop(pet, ORDER, command)
            end

            npc.play_sound(pet, "idle")
        end
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

    if all == "kill" then
        -- Every pet hears the words and there is one cursor: the first that asks gives it, and it is for all the pets.
        if pet.attend(speaker) then
            carry_out(all, speaker, pets_near(speaker))
        end

        return
    end

    -- Each pet that hears obeys for itself.
    if all ~= nil then
        carry_out(all, speaker, { serial })

        return
    end

    local named = command_of(keywords, NAMED)

    if named ~= nil and starts_with(text, npc.name(serial)) then
        carry_out(named, speaker, { serial })
    end
end

-- Food an owner dropped on its pet. True tells the server the item was taken, anything else gives it back. Only the
-- food the creature eats (data/pet_food.toml against its food in data/taming.toml) is taken, whole stack.
function pet_orders.feed(serial, giver, given)
    if owner_of(serial) ~= giver then
        return false
    end

    local before = pet.loyalty(serial) or 0
    local result = pet.feed(giver, serial, given)

    if result == PetFeedResultType.Fed or result == PetFeedResultType.AlreadyHappy or result == PetFeedResultType.Bonded then
        mobile.animate(serial, EAT)
        npc.play_sound(serial, "idle")

        if (pet.loyalty(serial) or 0) > before then
            npc.say_cliloc(serial, HAPPIER)
        end

        if result == PetFeedResultType.Bonded then
            mobile.message_cliloc(giver, BONDED)
        end

        return true
    end

    if result == PetFeedResultType.WrongFood then
        npc.say_cliloc(serial, SHIES_AWAY)
    end

    return false
end

return pet_orders
