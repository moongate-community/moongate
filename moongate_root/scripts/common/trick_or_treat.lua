-- ==============================================================================
-- Moongate - scripts/common/trick_or_treat.lua
--
-- What it is for:
--   The Halloween game, as ModernUO's Trick or Treat: while the event halloween
--   is on (data/schedule.toml), a player who says "trick or treat" within 4
--   tiles of a shopkeeper gets a candy, or a trick. Each shopkeeper rests for
--   5 to 10 minutes after answering. A shopkeeper script calls listen from its
--   on_speech.
--
-- Functions:
--   listen(serial, speaker, text)   the shopkeeper serial heard text from the
--                                   player speaker; true when it answered
--
-- Props it keeps on the shopkeeper:
--   trick_or_treat.next   the world.now() second at which it has candy again
-- ==============================================================================

local trick_or_treat = {}

-- The event of data/schedule.toml and the words that play it.
local EVENT = "halloween"
local WORDS = "trick or treat"

-- How far the shopkeeper hears, in tiles; how long it rests, in seconds.
local RANGE = 4
local REST_MIN = 300
local REST_MAX = 600

-- One in this many answers is a trick.
local TRICK_ONE_IN = 10

-- Messages of data/messages/<language>/moongate.toml.
local LINES = { 30230, 30231, 30232 }
local TRICK = 30233
local GOT_CANDY = 30234
local NO_CANDY = 30235

-- The templates of templates/items/food/halloween.toml.
local TREATS = {
    "0x468d_lollipops", "0x468e_lollipops", "0x468f_lollipops", "0x469e_wrapped_candy",
    "0x468c_jelly_beans", "0x469d_taffy", "0x4690_nougat_swirl"
}

-- The blood a trick leaves around the player, as ModernUO's Bleeding: 3 to 7 splashes.
local BLOOD = { "blood_splash_0x122a", "blood_splash_0x122b", "blood_splash_0x122c", "blood_splash_0x122d" }
local BLOOD_MIN = 3
local BLOOD_MAX = 7

local function near(serial, speaker)
    local here = npc.location(serial)
    local there = mobile.location(speaker)

    return here and there and here.map == there.map
        and math.abs(here.x - there.x) <= RANGE and math.abs(here.y - there.y) <= RANGE
end

local function pick(list)
    return list[math.random(#list)]
end

local function bleed(speaker)
    local where = mobile.location(speaker)

    for _ = 1, math.random(BLOOD_MIN, BLOOD_MAX) do
        item.create(pick(BLOOD), where.map, where.x + math.random(-1, 1), where.y + math.random(-1, 1), where.z)
    end
end

function trick_or_treat.listen(serial, speaker, text)
    if not text:lower():find(WORDS, 1, true) or not schedule.is_active(EVENT) then
        return false
    end

    if not near(serial, speaker) or mobile.is_dead(speaker) then
        return false
    end

    local now = world.now()

    if now < (npc.get_prop(serial, "trick_or_treat.next") or 0) then
        mobile.message(speaker, localization.get(NO_CANDY))

        return true
    end

    npc.set_prop(serial, "trick_or_treat.next", now + math.random(REST_MIN, REST_MAX))
    npc.look_at(serial, speaker)

    if math.random(TRICK_ONE_IN) == 1 then
        npc.say(serial, localization.get(TRICK))
        bleed(speaker)
    else
        npc.say(serial, localization.get(pick(LINES)))

        if item.give(speaker, pick(TREATS)) then
            mobile.message(speaker, localization.get(GOT_CANDY))
        end
    end

    return true
end

return trick_or_treat
