-- ==============================================================================
-- Moongate - scripts/skills/evaluating_intelligence.lua
--
-- What it is for:
--   The skill script of Evaluating Intelligence, as ModernUO's: the player picks a
--   mobile within 8 tiles and, if the skill check from 0 to 120 passes, reads how
--   intelligent it looks, in "he", "she" or "it"; from 76 points it reads how much
--   mana it has left too. The numbers it reads are off by up to 20 less one for
--   every 5 points of the skill (none at 100). A failed check reads that it cannot
--   judge its mental abilities. It waits the delay of data/skills.toml.
--
--   The texts of the client are told to the player as system messages, where
--   ModernUO shows them over the one examined.
--
-- Functions:
--   on_use(user)   the player user uses the skill
-- ==============================================================================

evaluating_intelligence = {}

-- The client's own texts.
local WHAT = 500906          -- What do you wish to evaluate?
local YOURSELF = 500910      -- Hmm, that person looks really silly.
local VENDOR = 500909        -- That person could probably calculate the cost of what you buy from them.
local NOT_LIVING = 500908    -- It looks smarter than a rock, but dumber than a piece of wood.

-- 1038169 + intelligence + body is "He/She/It looks [...]", intelligence from 0 to 10 and body 0 for a man, 11 for a
-- woman and 22 for the rest; 1038166 + body / 11 is "You cannot judge his/her/its mental abilities"; 1038202 + mana
-- is "That being is at [10, 20, ...] percent mental strength".
local LOOKS = 1038169
local CANNOT_JUDGE = 1038166
local MENTAL_STRENGTH = 1038202

local SIGHT = 8
local LEAST_FOR_MANA = 76

local FEMALE = 11
local NOT_HUMAN = 22

-- The most a number read is off, before the skill takes from it.
local MARGIN = 20
local MARGIN_STEP = 5

local function tenth(value)
    return math.max(0, math.min(10, math.floor(value / 10)))
end

local function body_of(who)
    if mobile.body_type(who) ~= BodyType.Human then
        return NOT_HUMAN
    end

    return mobile.is_female(who) and FEMALE or 0
end

function evaluating_intelligence.on_use(user)
    mobile.message_cliloc(user, WHAT)

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
            mobile.message_cliloc(user, NOT_LIVING)

            return
        end

        if mobile.notoriety(who) == "invulnerable" then
            mobile.message_cliloc(user, VENDOR)

            return
        end

        local at = mobile.location(who)
        local here = mobile.location(user)

        if at == nil or here == nil or at.map ~= here.map
            or math.max(math.abs(at.x - here.x), math.abs(at.y - here.y)) > SIGHT then
            return
        end

        local points = mobile.skills(user).evaluating_intelligence or 0
        local margin = math.max(0, MARGIN - math.floor(points / MARGIN_STEP))
        local intelligence = tenth(stats.intelligence + math.random(-margin, margin))
        local mana = tenth(math.floor(stats.mana * 100 / math.max(stats.mana_max, 1)) + math.random(-margin, margin))
        local body = body_of(who)

        if not skill.check(user, "evaluating_intelligence", 0, 120) then
            mobile.message_cliloc(user, CANNOT_JUDGE + math.floor(body / FEMALE))

            return
        end

        mobile.message_cliloc(user, LOOKS + intelligence + body)

        if points >= LEAST_FOR_MANA then
            mobile.message_cliloc(user, MENTAL_STRENGTH + mana)
        end
    end)
end
