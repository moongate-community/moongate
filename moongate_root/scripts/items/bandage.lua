-- ==============================================================================
-- Moongate - scripts/items/bandage.lua
--
-- What it is for:
--   The item script of the clean bandage, as ModernUO's classic Healing: a
--   player double clicks the bandage, picks who it is for, waits a few seconds,
--   and the one picked is healed, or raised if it is a ghost. A template uses it
--   with script_id = "bandage".
--
--   The bandage is used on someone within 1 tile of the healer and is taken out
--   of the stack when the healing begins. Who is healed:
--     alive   a player or an NPC that is hurt. The healing is rolled from the
--             Healing and Anatomy of the healer (Veterinary and Animal Lore for a
--             creature with the body of a monster or an animal): it works with a
--             chance of (Healing + 10) %, and then heals from
--             Anatomy / 5 + Healing / 5 + 3 up to Anatomy / 5 + Healing / 2 + 10,
--             one more point per 100 of a creature's hit points; a roll that
--             comes under 1 heals 1 and says the bandages barely helped.
--     ghost   a player that is dead: the healer needs 80 points of both skills
--             and a chance of (Healing - 68) / 50; if it works the ghost is asked
--             whether to come back, in the gump of the ankhs. There is no fame
--             loss: it is not a healer NPC.
--   The wait is 3 seconds for a healer with 100 dexterity or more, 4 from 40 and
--   5 under it, 5 more to raise a ghost; 9.4 - 0.6 * (dexterity - 120) / 10 on
--   itself. A second bandage of the same healer replaces the first. The healer
--   has to stay within 1 tile of who is healed and alive, or the healing is lost
--   (and the bandage with it). Both skills are tried for a rise after a healing,
--   whether the roll worked or not, and after a raise when both are at 80 or more.
--   There is no poison and no bleeding in the game yet, so no cure: bandages
--   heal, and raise.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the bandage serial
-- ==============================================================================

bandage = {}

-- How near the healer must be the bandage and who it heals.
local RANGE = 1

-- The client's own texts.
local TOO_FAR = 500295          -- You are too far away to do that.
local WHO = 500948              -- Who will you use the bandages on?
local NOT_DAMAGED = 500955      -- That being is not damaged!
local BEGIN = 500956            -- You begin applying the bandages.
local NOT_ENOUGH_CLOSE = 500963 -- You did not stay close enough to heal your target.
local DIED = 500962             -- You were unable to finish your work before you died.
local FINISHED = 500969         -- You finish applying the bandages.
local BARELY = 500968           -- You apply the bandages, but they barely help.
local CANNOT = 500970           -- Bandages can not be used on that.
local RAISED = 500965           -- You are able to resurrect your patient.
local NOT_RAISED = 500966       -- You are unable to resurrect your patient.
local ATTEMPTING = 1008078      -- {0} : Attempting to heal you.

local HEAL_SOUND = 0x57
local RAISE_SOUND = 0x214

-- What it takes to raise a ghost: the least skills, the chance's offset and span, and the extra seconds.
local RAISE_SKILL = 80
local RAISE_FROM = 68
local RAISE_SPAN = 50
local RAISE_WAIT = 5

-- The bandage each healer is applying: a new one replaces the one before.
local applying = {}

local function skills_of(healer, patient)
    local skills = mobile.skills(healer)

    -- A creature is a case for the veterinary: a player or a human is healed by the healing.
    if not mobile.is_player(patient) and mobile.body_type(patient) ~= BodyType.Human then
        return skills.veterinary or 0, skills.animal_lore or 0, "veterinary", "animal_lore"
    end

    return skills.healing or 0, skills.anatomy or 0, "healing", "anatomy"
end

-- The seconds the healing takes, ModernUO's classic ones.
local function delay_of(healer, patient)
    local dex = mobile.stats(healer).dexterity
    local ghost = mobile.is_dead(patient) and RAISE_WAIT or 0

    if healer == patient then
        return 9.4 + 0.6 * ((120 - dex) / 10)
    elseif dex >= 100 then
        return 3 + ghost
    elseif dex >= 40 then
        return 4 + ghost
    end

    return 5 + ghost
end

-- Whether the healer has the bandage at hand: it carries it, or it lies within a tile.
local function at_hand(serial, user)
    return item.owner(serial) == user or item.in_range(serial, user, RANGE)
end

-- Whether the patient is within the range of the healer, on the same map.
local function near(healer, patient)
    local at = mobile.location(patient)
    local here = mobile.location(healer)

    return at ~= nil
        and here ~= nil
        and here.map == at.map
        and math.max(math.abs(here.x - at.x), math.abs(here.y - at.y)) <= RANGE
end

local function try_for_rise(healer, primary, secondary)
    skill.check(healer, secondary, 0, 120)
    skill.check(healer, primary, 0, 120)
end

local function heal(healer, patient, hits, hits_max, healing, anatomy)
    if (healing + 10) / 100 <= math.random() then
        mobile.message_cliloc(healer, BARELY)

        return
    end

    local least = anatomy / 5 + healing / 5 + 3
    local most = anatomy / 5 + healing / 2 + 10
    local amount = least + math.random() * (most - least)

    if mobile.body_type(patient) ~= BodyType.Human and not mobile.is_player(patient) then
        amount = amount + hits_max / 100
    end

    amount = math.floor(amount)

    if amount < 1 then
        amount = 1
        mobile.message_cliloc(healer, BARELY)
    else
        mobile.message_cliloc(healer, FINISHED)
    end

    mobile.set_stats(patient, { hits = math.min(hits + amount, hits_max) })
    mobile.play_sound(patient, HEAL_SOUND)
end

local function raise(healer, patient, healing, anatomy)
    local chance = (healing - RAISE_FROM) / RAISE_SPAN

    if healing < RAISE_SKILL or anatomy < RAISE_SKILL or chance <= math.random() then
        mobile.message_cliloc(healer, NOT_RAISED)

        return false
    end

    mobile.message_cliloc(healer, RAISED)
    mobile.play_sound(patient, RAISE_SOUND)
    gump.open(patient, "resurrect", { bandager = healer })

    return true
end

-- What happens when the bandages are done: the healer has stayed near and is alive.
local function finish(healer, patient)
    if mobile.is_dead(healer) then
        mobile.message_cliloc(healer, DIED)

        return
    end

    if not near(healer, patient) then
        mobile.message_cliloc(healer, NOT_ENOUGH_CLOSE)

        return
    end

    local healing, anatomy, primary, secondary = skills_of(healer, patient)

    if mobile.is_dead(patient) then
        raise(healer, patient, healing, anatomy)

        -- The skills are tried for a rise only when both are high enough to raise.
        if healing >= RAISE_SKILL and anatomy >= RAISE_SKILL then
            try_for_rise(healer, primary, secondary)
        end

        return
    end

    local stats = mobile.stats(patient)

    if stats.hits >= stats.hits_max then
        mobile.message_cliloc(healer, FINISHED)

        return
    end

    heal(healer, patient, stats.hits, stats.hits_max, healing, anatomy)
    try_for_rise(healer, primary, secondary)
end

-- Begins healing the patient with the bandage; false when nothing began.
local function begin(serial, healer, patient)
    local stats = mobile.stats(patient)

    if stats == nil then
        mobile.message_cliloc(healer, CANNOT)

        return
    end

    if not mobile.is_dead(patient) and stats.hits >= stats.hits_max then
        mobile.message_cliloc(healer, NOT_DAMAGED)

        return
    end

    if not near(healer, patient) then
        mobile.message_cliloc(healer, TOO_FAR)

        return
    end

    if not item.consume(serial) then
        return
    end

    local token = {}
    applying[healer] = token

    mobile.message_cliloc(healer, BEGIN)

    if healer ~= patient then
        mobile.message_cliloc(patient, ATTEMPTING, mobile.name(healer) or "")
    end

    timer.after(delay_of(healer, patient), function()
        -- A new bandage of the same healer replaced this one.
        if applying[healer] ~= token then
            return
        end

        applying[healer] = nil
        finish(healer, patient)
    end)
end

-- Called when a player double clicks the bandage.
function bandage.on_use(serial, user)
    if not at_hand(serial, user) then
        mobile.message_cliloc(user, TOO_FAR)

        return true
    end

    mobile.message_cliloc(user, WHO)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        if mobile.location(picked.serial) == nil then
            mobile.message_cliloc(user, CANNOT)

            return
        end

        if not at_hand(serial, user) then
            mobile.message_cliloc(user, TOO_FAR)

            return
        end

        begin(serial, user, picked.serial)
    end)

    return true
end
