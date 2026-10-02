-- ==============================================================================
-- Moongate - scripts/items/keyword_teleport.lua
--
-- What it is for:
--   The item script of the teleporters that answer a word (item template
--   decoration_keyword_teleporter), as ModernUO's KeywordTeleporter: a player
--   who says the word close enough stands on the destination, such as the
--   mantra of a shrine. Walking onto one does nothing. Players never see the
--   item; staff does.
--
-- Props it reads:
--   substring      the word or sentence to say; found anywhere in what the
--                  player says, in any case
--   keyword        a speech keyword number of the client, instead of or beside
--                  the substring
--   range          how many cells away the player may stand; 0, the default,
--                  is the teleporter's own cell
--   delay          how long after the word the teleport happens, as
--                  "hours:minutes:seconds" or a number of seconds; the player
--                  must still be in range then
--   teleport.x, teleport.y, teleport.z   the destination; a teleporter without
--                                        all three does nothing
--   teleport.map   the destination map (a MapType number); a teleporter to
--                  another map does nothing yet
--   active         false turns the teleporter off
--   source_effect  true shows a puff of smoke where the player left
--   dest_effect    true shows a puff of smoke where the player arrived
--   sound_id       a sound played at the destination after the teleport
--
-- Functions:
--   on_speech(serial, speaker, text, keywords)   a player said something
--                                                within 15 cells
-- ==============================================================================

keyword_teleport = {}

-- The decoration files carry the flags as text.
local function is_on(value)
    return value == true or value == "true"
end

-- "0:0:1" is one second; a number is seconds already.
local function seconds(delay)
    if type(delay) == "number" then
        return delay
    end

    if type(delay) ~= "string" then
        return 0
    end

    local _, _, hours, minutes, secs = delay:find("^(%d+):(%d+):(%d+)")

    if not hours then
        return 0
    end

    return tonumber(hours) * 3600 + tonumber(minutes) * 60 + tonumber(secs)
end

-- Where the player stands when it is on the teleporter's map and within its range; nil otherwise.
local function in_range(serial, who)
    local here = item.location(serial)
    local at = mobile.location(who)

    if not here or not at or here.map ~= at.map then
        return nil
    end

    local range = item.get_prop(serial, "range") or 0

    if math.abs(at.x - here.x) > range or math.abs(at.y - here.y) > range then
        return nil
    end

    return at
end

local function matches(serial, text, keywords)
    local keyword = item.get_prop(serial, "keyword")

    if type(keyword) == "number" and keyword >= 0 then
        for _, heard in ipairs(keywords or {}) do
            if heard == keyword then
                return true
            end
        end
    end

    local substring = item.get_prop(serial, "substring")

    return type(substring) == "string" and substring ~= "" and text:lower():find(substring:lower(), 1, true) ~= nil
end

-- As ModernUO: after the delay the player must still stand in range.
local function teleport(serial, who)
    local from = in_range(serial, who)

    if not from then
        return
    end

    local x = item.get_prop(serial, "teleport.x")
    local y = item.get_prop(serial, "teleport.y")
    local z = item.get_prop(serial, "teleport.z")

    if not (x and y and z) then
        return
    end

    local map = item.get_prop(serial, "teleport.map")

    if map and map ~= from.map then
        return
    end

    if is_on(item.get_prop(serial, "source_effect")) then
        effect.at(from.map, from.x, from.y, from.z, EffectGraphicType.Smoke)
    end

    if mobile.teleport(who, x, y, z) then
        if is_on(item.get_prop(serial, "dest_effect")) then
            effect.at(from.map, x, y, z, EffectGraphicType.Smoke)
        end

        local sound = item.get_prop(serial, "sound_id")

        if sound then
            mobile.play_sound(who, sound)
        end
    end
end

-- Called when a player says something within 15 cells.
function keyword_teleport.on_speech(serial, speaker, text, keywords)
    if item.get_prop(serial, "active") == false then
        return
    end

    if not in_range(serial, speaker) or not matches(serial, text, keywords) then
        return
    end

    local wait_for = seconds(item.get_prop(serial, "delay"))

    if wait_for > 0 then
        timer.after(wait_for, function()
            teleport(serial, speaker)
        end)
    else
        teleport(serial, speaker)
    end
end
