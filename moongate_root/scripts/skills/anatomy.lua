-- ==============================================================================
-- Moongate - scripts/skills/anatomy.lua
--
-- What it is for:
--   The skill script of Anatomy, as ModernUO's: the player picks a mobile within
--   8 tiles and, if the skill check from 0 to 100 passes, reads how strong and how
--   dexterous it looks; from 65 points it reads how much endurance it has left too.
--   The numbers it reads are off by up to 25 less one for every 4 points of the
--   skill (none at 100). A failed check reads "You can not quite get a sense of
--   their physical characteristics.". It waits the delay of data/skills.toml.
--
--   The texts of the client are told to the player as system messages, where
--   ModernUO shows them over the one examined.
--
-- Functions:
--   on_use(user)   the player user uses the skill
-- ==============================================================================

anatomy = {}

-- The client's own texts.
local WHOM = 500321          -- Whom shall I examine?
local YOURSELF = 500324      -- You know yourself quite well enough already.
local CANNOT = 500326        -- That can not be inspected.
local NOT_ALIVE = 500323     -- Only living things have anatomies!
local CAN_NOT_SENSE = 1042666 -- You can not quite get a sense of their physical characteristics.

-- 1038045 + strength * 11 + dexterity is "That looks [strong] and [dexterous]", each from 0 to 10; 1038303 + endurance
-- is "That being is at [10, 20, ...] percent endurance".
local LOOKS = 1038045
local ENDURANCE = 1038303

local SIGHT = 8
local LEAST_FOR_ENDURANCE = 65

-- The most a number read is off, before the skill takes from it.
local MARGIN = 25
local MARGIN_STEP = 4

local function off_by(margin)
    return math.random(-margin, margin)
end

local function tenth(value)
    return math.max(0, math.min(10, math.floor(value / 10)))
end

function anatomy.on_use(user)
    mobile.message_cliloc(user, WHOM)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        local who = picked.serial

        if who == user then
            mobile.message_cliloc(user, YOURSELF)

            return
        end

        local stats = mobile.stats(who)

        if stats == nil then
            mobile.message_cliloc(user, NOT_ALIVE)

            return
        end

        if mobile.notoriety(who) == "invulnerable" then
            mobile.message_cliloc(user, CANNOT)

            return
        end

        local at = mobile.location(who)
        local here = mobile.location(user)

        if at == nil or here == nil or at.map ~= here.map
            or math.max(math.abs(at.x - here.x), math.abs(at.y - here.y)) > SIGHT then
            return
        end

        local points = mobile.skills(user).anatomy or 0
        local margin = math.max(0, MARGIN - math.floor(points / MARGIN_STEP))
        local strength = tenth(stats.strength + off_by(margin))
        local dexterity = tenth(stats.dexterity + off_by(margin))
        local stamina = tenth(math.floor(stats.stamina * 100 / math.max(stats.stamina_max, 1)) + off_by(margin))

        if not skill.check(user, "anatomy", 0, 100) then
            mobile.message_cliloc(user, CAN_NOT_SENSE)

            return
        end

        mobile.message_cliloc(user, LOOKS + strength * 11 + dexterity)

        if points >= LEAST_FOR_ENDURANCE then
            mobile.message_cliloc(user, ENDURANCE + stamina)
        end
    end)
end
