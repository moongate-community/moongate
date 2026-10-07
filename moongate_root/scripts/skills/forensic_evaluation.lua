-- ==============================================================================
-- Moongate - scripts/skills/forensic_evaluation.lua
--
-- What it is for:
--   The skill script of Forensic Evaluation, as ModernUO's, as far as the game goes:
--   the player is revealed if hidden, picks a corpse or a mobile within 10 tiles.
--   On a corpse, the skill check from 0 to 100 that passes tells whom a human
--   corpse was killed by ("no one" when it was not by someone). On a mobile, the
--   check from 40 to 100 that passes says there is nothing unusual: there is no
--   thieves' guild, so no thief to be found. A failed check reads "You cannot
--   determine anything useful.". It waits the delay of data/skills.toml.
--
--   Not there yet: who disturbed the corpse, who has studied it already, and the
--   locks that remember who picked them.
--
-- Functions:
--   on_use(user)   the player user uses the skill
-- ==============================================================================

forensic_evaluation = {}

-- The client's own texts.
local SHOW_ME = 501000        -- Show me the crime.
local NOTHING_USEFUL = 501001 -- You cannot determine anything useful.
local NOTHING_UNUSUAL = 501003 -- You notice nothing unusual.
local KILLED_BY = 1042751     -- This person was killed by ~1_KILLER_NAME~

local SIGHT = 10
local CORPSE_GRAPHIC = 0x2006

-- The bodies of the human races, so that a human corpse tells who killed it.
local HUMAN_BODIES = {
    [400] = true, [401] = true, [605] = true, [606] = true, [666] = true, [667] = true, [694] = true, [695] = true
}

local function within_sight(user, serial)
    local here = mobile.location(user)
    local at = mobile.location(serial) or item.location(serial)

    return here ~= nil and at ~= nil and at.map == here.map
        and math.max(math.abs(at.x - here.x), math.abs(at.y - here.y)) <= SIGHT
end

local function killer_of(corpse)
    local killer = item.get_prop(corpse, "corpse.killer")

    if killer == nil then
        return "no one"
    end

    return mobile.name(killer) or "someone"
end

function forensic_evaluation.on_use(user)
    mobile.set_hidden(user, false)
    mobile.message_cliloc(user, SHOW_ME)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        local what = picked.serial

        if mobile.location(what) ~= nil then
            if within_sight(user, what) then
                if skill.check(user, "forensic_evaluation", 40, 100) then
                    mobile.message_cliloc(user, NOTHING_UNUSUAL)
                else
                    mobile.message_cliloc(user, NOTHING_USEFUL)
                end
            end

            return
        end

        if item.item_id(what) ~= CORPSE_GRAPHIC or not within_sight(user, what) then
            return
        end

        if not skill.check(user, "forensic_evaluation", 0, 100) then
            mobile.message_cliloc(user, NOTHING_USEFUL)

            return
        end

        local body = item.get_prop(what, "corpse.body")

        if HUMAN_BODIES[body] or item.get_prop(what, "corpse.owner") ~= nil then
            mobile.message_cliloc(user, KILLED_BY, killer_of(what))
        end
    end)
end
