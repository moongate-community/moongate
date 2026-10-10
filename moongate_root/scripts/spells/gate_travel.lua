-- ==============================================================================
-- Moongate - scripts/spells/gate_travel.lua
--
-- What it is for:
--   The seventh circle spell Gate Travel: a gate opens where the caster stands
--   and another at the place a marked recall rune holds, each leading to the
--   other, for thirty seconds (items/moongate.lua takes whoever steps onto one
--   and takes the gate away when its time is up; Dispel Field takes both).
--   The rune is an item of the template recall_rune (see Recall). It is refused,
--   before anything is spent, for what is not a rune, a rune not marked, a
--   criminal, a rune of another map, a region that does not let a gate out of it or into it (the gate_out
--   and gate_in flags of data/regions), a place that a mobile or an item fills,
--   and a gate that is there already at either place. Not built: the check for a
--   fight in progress (the engine has no combat heat to ask), the sigil and the
--   runebook. Called by the spell service with the caster, the target
--   ({ kind = "item", serial }) and the data of the spell.
--
-- Functions:
--   gate_travel.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   gate_travel.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

gate_travel = {}

local RUNE_TEMPLATE = "recall_rune"
local GATE = "magic_gate"
local NOT_A_RUNE = 502357       -- I can not recall from that object.
local NOT_MARKED = 501805       -- That rune is not yet marked.
local CRIMINAL = 1005561        -- Thou'rt a criminal and cannot escape so easily.
local OTHER_FACET = 1005570     -- You can not gate to another facet.
local BLOCKED = 501942          -- That location is blocked.
local NOT_ALLOWED = 1019004     -- You are not allowed to travel there.
local CANNOT_FROM = 501802      -- Thy spell doth not appear to work...
local ALREADY_THERE = 1071242   -- There is already a gate there.
local OPENED = 501024           -- You open a magical gate to another location
local SECONDS = 30
local SOUND = 0x20E

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

-- Whether a gate lies at the place already, of this spell or another.
local function gate_at(map, x, y)
    for _, found in ipairs(world.items_in_range(map, x, y, 0)) do
        local template = item.template(found)

        if template == GATE or template == "moongate" then
            return true
        end
    end

    return false
end

function gate_travel.check(caster, target, info)
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

    if place.map ~= here.map then
        return OTHER_FACET
    end

    if not world.travel_allowed(here.map, here.x, here.y, here.z, "gate_out") then
        return CANNOT_FROM
    end

    local z = world.standing_z(place.map, place.x, place.y, place.z)

    if not z or not world.can_fit(place.map, place.x, place.y, z, caster) then
        return BLOCKED
    end

    if not world.travel_allowed(place.map, place.x, place.y, z, "gate_in") then
        return NOT_ALLOWED
    end

    if gate_at(here.map, here.x, here.y) or gate_at(place.map, place.x, place.y) then
        return ALREADY_THERE
    end
end

local function open(map, x, y, z, to)
    local gate = item.create(GATE, map, x, y, z)

    if not gate then
        return nil
    end

    item.set_prop(gate, "teleport.x", to.x)
    item.set_prop(gate, "teleport.y", to.y)
    item.set_prop(gate, "teleport.z", to.z)
    item.set_prop(gate, "teleport.map", to.map)
    item.start_timer(gate, "expire", SECONDS)

    return gate
end

function gate_travel.cast(caster, target, info)
    local place = marked_place(target.serial)
    local here = mobile.location(caster)

    if not place or not here then
        return
    end

    local z = world.standing_z(place.map, place.x, place.y, place.z) or place.z

    mobile.message_cliloc(caster, OPENED)
    world.play_sound(here.map, here.x, here.y, here.z, info.sound ~= 0 and info.sound or SOUND)

    local first = open(here.map, here.x, here.y, here.z, { x = place.x, y = place.y, z = z, map = place.map })

    world.play_sound(place.map, place.x, place.y, z, info.sound ~= 0 and info.sound or SOUND)

    local second = open(place.map, place.x, place.y, z, { x = here.x, y = here.y, z = here.z, map = here.map })

    -- Each knows the other, so that a Dispel Field takes both.
    if first and second then
        item.set_prop(first, "gate.partner", second)
        item.set_prop(second, "gate.partner", first)
    end
end
