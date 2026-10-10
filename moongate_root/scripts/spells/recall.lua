-- ==============================================================================
-- Moongate - scripts/spells/recall.lua
--
-- What it is for:
--   The fourth circle spell Recall: the caster is carried to the place a recall
--   rune is marked with, with the sound at both places. The rune is an item of
--   the template recall_rune whose props say where: rune.marked (true), and
--   rune.x, rune.y, rune.z and rune.map. It is refused, before anything is spent,
--   for what is not a rune, a rune not marked, a criminal, a caster too loaded
--   to move, a rune of another map, a place no one can stand on or that a
--   mobile or an item fills, and a region that does not let a recall out of it
--   or into it (the recall_out and
--   recall_in flags of data/regions). Not built: a check for a fight in
--   progress (the engine has no combat heat to ask) and the pets that
--   follow. Called by the spell service with the caster, the target
--   ({ kind = "item", serial }) and the data of the spell.
--
-- Functions:
--   recall.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   recall.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

recall = {}

local RUNE_TEMPLATE = "recall_rune"
local NOT_A_RUNE = 502357       -- I can not recall from that object.
local NOT_MARKED = 501805       -- That rune is not yet marked.
local CRIMINAL = 1005561        -- Thou'rt a criminal and cannot escape so easily.
local TOO_ENCUMBERED = 502359   -- Thou art too encumbered to move.
local OTHER_FACET = 1005569     -- You can not recall to another facet.
local BLOCKED = 501942          -- That location is blocked.
local NOT_ALLOWED = 1019004     -- You are not allowed to travel there.
local CANNOT_FROM = 502361      -- You cannot teleport into that area from here.
local SOUND = 0x1FC

local function marked_place(rune)
    if item.get_prop(rune, "rune.marked") ~= true then
        return nil
    end

    local x = item.get_prop(rune, "rune.x")
    local y = item.get_prop(rune, "rune.y")
    local z = item.get_prop(rune, "rune.z")
    local map = item.get_prop(rune, "rune.map")

    if x and y and z and map then
        return { x = x, y = y, z = z, map = map }
    end
end

function recall.check(caster, target, info)
    local rune = target.serial

    if item.template(rune) ~= RUNE_TEMPLATE then
        return NOT_A_RUNE
    end

    local place = marked_place(rune)

    if not place then
        return NOT_MARKED
    end

    local here = mobile.location(caster)

    if not here then
        return false
    end

    if mobile.criminal(caster) then
        return CRIMINAL
    end

    if (mobile.weight(caster) or 0) > (mobile.max_weight(caster) or math.huge) then
        return TOO_ENCUMBERED
    end

    if place.map ~= here.map then
        return OTHER_FACET
    end

    if not world.travel_allowed(here.map, here.x, here.y, here.z, "recall_out") then
        return CANNOT_FROM
    end

    local z = world.standing_z(place.map, place.x, place.y, place.z)

    -- Nor where a mobile stands, or an impassable item lies on the ground, such as a shut door.
    if not z or not world.can_fit(place.map, place.x, place.y, z, caster) then
        return BLOCKED
    end

    if not world.travel_allowed(place.map, place.x, place.y, z, "recall_in") then
        return NOT_ALLOWED
    end
end

function recall.cast(caster, target, info)
    local place = marked_place(target.serial)
    local here = mobile.location(caster)

    if not place or not here then
        return
    end

    local z = world.standing_z(place.map, place.x, place.y, place.z) or place.z

    mobile.play_sound(caster, SOUND)

    if mobile.teleport(caster, place.x, place.y, z, place.map) then
        mobile.play_sound(caster, SOUND)
    end
end
