-- ==============================================================================
-- Moongate - scripts/skills/stealth.lua
--
-- What it is for:
--   The skill script of Stealth, as ModernUO's classic one: a hidden player uses
--   the skill and, if it passes, may take a few steps without being shown, up to
--   a tenth of its Stealth in steps, at least one. Running always shows it. The
--   steps are the server's (mobile.set_stealth_steps); hiding or being shown
--   again clears them.
--
--   Before the check: a player that is not hidden is told to hide first; a player
--   with under 80 points of Hiding is not hidden well enough, and one wearing an
--   armor rating of 26 or more could not hope to move quietly: both are shown.
--   The check runs from -20 to 80 points, each moved up by twice the armor
--   rating. A failed check shows the player. Whatever happens, the skill waits
--   ten seconds.
--
-- Functions:
--   on_use(user)   the player user uses the skill; returns the seconds to wait
-- ==============================================================================

stealth = {}

-- The client's own texts.
local HIDE_FIRST = 502725     -- You must hide first
local NOT_HIDDEN_WELL = 502726 -- You are not hidden well enough.  Become better at hiding.
local TOO_MUCH_ARMOR = 502727 -- You could not hope to move quietly wearing this much armor.
local BEGIN = 502730          -- You begin to move quietly.
local FAILED = 502731         -- You fail in your attempt to move unnoticed.
local MOUNTED = 500837        -- You cannot stealth while mounted.

-- The Hiding it takes, the armor rating that is too much, and what the armor does to the try.
local HIDING_REQUIRED = 80
local ARMOR_LIMIT = 26
local LEAST = -20
local MOST = 80
local ARMOR_FACTOR = 2

-- A tenth of the skill is the number of steps, the least being one.
local POINTS_PER_STEP = 10

-- Seconds before another skill.
local DELAY = 10

local function show(user)
    mobile.set_hidden(user, false)
end

-- Called when a player uses the skill.
function stealth.on_use(user)
    -- A rider makes too much noise: it sneaks on foot only.
    if mobile.is_mounted(user) then
        mobile.message_cliloc(user, MOUNTED)

        return
    end

    local flags = mobile.flags(user)

    if flags == nil or not flags.hidden then
        mobile.message_cliloc(user, HIDE_FIRST)

        return DELAY
    end

    local skills = mobile.skills(user)

    if (skills.hiding or 0) < HIDING_REQUIRED then
        mobile.message_cliloc(user, NOT_HIDDEN_WELL)
        show(user)

        return DELAY
    end

    local armor = combat.armor_rating(user) or 0

    if armor >= ARMOR_LIMIT then
        mobile.message_cliloc(user, TOO_MUCH_ARMOR)
        show(user)

        return DELAY
    end

    if skill.check(user, "stealth", LEAST + armor * ARMOR_FACTOR, MOST + armor * ARMOR_FACTOR) then
        mobile.set_stealth_steps(user, math.max(math.floor((skills.stealth or 0) / POINTS_PER_STEP), 1))
        mobile.message_cliloc(user, BEGIN)

        return DELAY
    end

    mobile.message_cliloc(user, FAILED)
    show(user)

    return DELAY
end
