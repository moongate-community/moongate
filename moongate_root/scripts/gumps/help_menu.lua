-- ==============================================================================
-- Moongate - scripts/gumps/help_menu.lua
--
-- What it is for:
--   The script of the help gump (templates/gumps/help_menu.xml), opened by the
--   Help button of the paperdoll. "I am stuck" takes the character to the nearest
--   starting city after it stood still for ultima.help.stuck_wait_seconds, and
--   asks the player to wait ultima.help.stuck_cooldown_minutes before the next
--   time (the staff does not wait). It is refused in jail and while fighting,
--   and both are checked again when the wait is over. A ghost may ask.
--
-- Functions:
--   stuck(player, response, args)     the "I am stuck" button
--   commands(player, response, args)  runs .help as the player
--   rules(player, response, args)     tells the rules of the server
--   call(player, response, args)      opens help_page_kind, the first step of Call a game master
-- ==============================================================================

help_menu = {}

-- Moongate messages (data/messages/<language>/moongate.toml).
local in_jail_message = 30192
local fighting_message = 30193
local waiting_message = 30194
local pause_message = 30195
local stand_still_message = 30196
local moved_message = 30197
local taken_message = 30198
local no_city_message = 30199
local rules_message = 30200

-- The prop that keeps, in seconds since 1970, when the player may ask again; it survives a restart.
local pause_key = "help.stuck_until"

-- The players whose request waits for its timer, by serial.
local waiting = {}

local function say(player, id, ...)
    mobile.message(player, localization.get(id, ...))
end

-- Why the player may not ask now, as a message id, or nil when it may.
local function refusal(player)
    if jail.sentence(player) ~= nil then
        return in_jail_message
    end

    if combat.target(player) ~= nil then
        return fighting_message
    end

    return nil
end

local function same_spot(a, b)
    return a ~= nil and b ~= nil and a.x == b.x and a.y == b.y and a.z == b.z and a.map == b.map
end

function help_menu.stuck(player, response, args)
    if waiting[player] then
        say(player, waiting_message)

        return
    end

    local refused = refusal(player)

    if refused ~= nil then
        say(player, refused)

        return
    end

    local staff = world.is_staff(player)
    local until_when = mobile.get_prop(player, pause_key)

    if not staff and type(until_when) == "number" and until_when > world.now() then
        say(player, pause_message, math.ceil((until_when - world.now()) / 60))

        return
    end

    local city = help.nearest_city(player)

    if city == nil then
        say(player, no_city_message)

        return
    end

    local settings = help.settings()
    local from = mobile.location(player)

    waiting[player] = true
    say(player, stand_still_message, settings.wait_seconds, city.town)

    timer.after(settings.wait_seconds, function()
        waiting[player] = nil

        local here = mobile.location(player)

        -- Logged out during the wait: nothing to do.
        if here == nil then
            return
        end

        if not same_spot(from, here) then
            say(player, moved_message)

            return
        end

        local refused_now = refusal(player)

        if refused_now ~= nil then
            say(player, refused_now)

            return
        end

        -- The map of the city may not be loaded: nobody was moved, so no pause is spent.
        if not mobile.teleport(player, city.x, city.y, city.z, city.map) then
            say(player, no_city_message)

            return
        end

        say(player, taken_message, city.town)
        log.info("Player {Player} was moved by I am stuck from {From} to {Town}", player,
            string.format("%d,%d,%d", from.x, from.y, from.z), city.town)

        if not staff then
            mobile.set_prop(player, pause_key, world.now() + settings.cooldown_minutes * 60)
        end
    end)
end

function help_menu.commands(player, response, args)
    commands.execute_as(player, "help")
end

function help_menu.rules(player, response, args)
    say(player, rules_message)
end

function help_menu.call(player, response, args)
    gump.open(player, "help_page_kind")
end
