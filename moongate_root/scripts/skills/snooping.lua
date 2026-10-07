-- ==============================================================================
-- Moongate - scripts/skills/snooping.lua
--
-- What it is for:
--   The script of Snooping, as ModernUO's. The skill is not used from the skill
--   window: it runs when a player double clicks the backpack of another mobile,
--   the server calls on_snoop(user, owner, container) here instead of opening it.
--
--   The player must stand within a tile of the owner (staff need not). Nothing
--   happens for a dead owner. A game master or administrator cannot be snooped,
--   and neither can who the rules protect: "You cannot perform negative acts on
--   your target." Anyone can be snooped on Felucca; elsewhere an NPC in a
--   guarded region only when it is not human, or attackable or a murderer.
--
--   A player who is not staff loses 4 karma, and may be noticed by the players
--   within 8 tiles, who read "You notice <name> attempting to peek into
--   <owner>'s belongings.": always under 100 points of Snooping, with a chance
--   of the points in a hundred of passing unnoticed. Then the skill is tried from
--   0 to 100 points: a success shows the backpack to the player; a failure reads
--   "You failed to peek into the container." and may show the player, more likely
--   the less it has of Hiding (half its points in a hundred of staying hidden).
--   Staff always see.
--
--   Not there yet: the traps of containers.
--
-- Functions:
--   on_snoop(user, owner, container)   the player user double clicked the
--                                      backpack container of the mobile owner
-- ==============================================================================

snooping = {}

-- The client's own texts.
local TOO_FAR = 500446        -- That is too far away.
local NEGATIVE_ACTS = 1001018 -- You cannot perform negative acts on your target.
local FAILED = 500210         -- You failed to peek into the container.

local REACH = 1
local NOTICE_RANGE = 8
local KARMA_COST = 4

local function distance(a, b)
    return math.max(math.abs(a.x - b.x), math.abs(a.y - b.y))
end

-- Whether the rules let the player snoop the owner, as ModernUO's CheckSnoopAllowed.
local function allowed(owner, place)
    if mobile.is_player(owner) then
        return mobile.notoriety(owner) ~= "invulnerable"
    end

    if place.map == MapType.Felucca or not world.is_guarded(place.map, place.x, place.y, place.z) then
        return true
    end

    local notoriety = mobile.notoriety(owner)

    return mobile.body_type(owner) ~= BodyType.Human or notoriety == "attackable" or notoriety == "enemy"
        or notoriety == "murderer"
end

-- The players near enough tell who peeks, most of the time while the skill is low.
local function maybe_noticed(user, owner, points)
    if points >= 100 or points >= math.random() * 100 then
        return
    end

    local here = mobile.location(user)
    local text = "You notice " .. (mobile.name(user) or "someone") .. " attempting to peek into "
        .. (mobile.name(owner) or "someone") .. "'s belongings."

    for _, who in ipairs(world.mobiles_in_range(here.map, here.x, here.y, NOTICE_RANGE)) do
        if who ~= user and mobile.is_player(who) then
            mobile.message(who, text)
        end
    end
end

-- Called when a player double clicks the backpack of another mobile.
function snooping.on_snoop(user, owner, container)
    local staff = world.is_staff(user)
    local here = mobile.location(user)
    local place = mobile.location(owner)

    if here == nil or place == nil then
        return
    end

    if not staff and (here.map ~= place.map or distance(here, place) > REACH) then
        mobile.message_cliloc(user, TOO_FAR)

        return
    end

    if mobile.is_dead(owner) then
        return
    end

    if world.is_staff(owner) or not allowed(owner, place) then
        mobile.message_cliloc(user, NEGATIVE_ACTS)

        return
    end

    if not staff then
        maybe_noticed(user, owner, mobile.skills(user).snooping or 0)

        local stats = mobile.stats(user)

        mobile.set_stats(user, { karma = stats.karma - KARMA_COST })
    end

    if staff or skill.check(user, "snooping", 0, 100) then
        item.show_contents(container, user)

        return
    end

    mobile.message_cliloc(user, FAILED)

    if (mobile.skills(user).hiding or 0) / 2 < math.random() * 100 then
        mobile.set_hidden(user, false)
    end
end
