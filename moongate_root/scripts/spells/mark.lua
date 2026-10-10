-- ==============================================================================
-- Moongate - scripts/spells/mark.lua
--
-- What it is for:
--   The sixth circle spell Mark: the recall rune the caster picks, which it must
--   carry in its backpack, is marked with the place where the caster stands,
--   as the staff command .mark_rune does (the props rune.marked, rune.x, rune.y,
--   rune.z, rune.map, and the name "a recall rune for Britain"). A rune can be
--   marked again. It is refused, before anything is spent, for what is not a
--   rune, a rune that is not in the backpack, a region that does not let a rune
--   be marked in it (the mark flag of data/regions) and a place where a mobile
--   stands that is not the caster's. Called by the spell service with the caster,
--   the target ({ kind = "item", serial }) and the data of the spell.
--
-- Functions:
--   mark.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   mark.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

mark = {}

local RUNE_TEMPLATE = "recall_rune"
local NOT_A_RUNE = 501797        -- I cannot mark that object.
local NOT_IN_PACK = 1062422      -- You must have the rune in your pack to mark it.
local NOT_ALLOWED = 501802       -- Thy spell doth not appear to work...
local EFFECT = 14201
local EFFECT_DURATION = 16

-- Whether the item lies in the caster's backpack, at any depth.
local function in_pack(caster, serial)
    local pack = mobile.backpack(caster)
    local holder = serial

    for _ = 1, 8 do
        holder = item.container(holder)

        if holder == nil then
            return false
        end

        if holder == pack then
            return true
        end
    end

    return false
end

function mark.check(caster, target, info)
    if item.template(target.serial) ~= RUNE_TEMPLATE then
        return NOT_A_RUNE
    end

    local here = mobile.location(caster)

    if not here then
        return false
    end

    if not world.travel_allowed(here.map, here.x, here.y, here.z, "mark") then
        return NOT_ALLOWED
    end

    if not in_pack(caster, target.serial) then
        return NOT_IN_PACK
    end
end

function mark.cast(caster, target, info)
    local here = mobile.location(caster)
    local rune = target.serial

    if not here then
        return
    end

    item.set_prop(rune, "rune.marked", true)
    item.set_prop(rune, "rune.x", here.x)
    item.set_prop(rune, "rune.y", here.y)
    item.set_prop(rune, "rune.z", here.z)
    item.set_prop(rune, "rune.map", here.map)
    item.set_name(rune, "a recall rune for " .. world.rune_place(here.map, here.x, here.y, here.z))

    mobile.play_sound(caster, info.sound ~= 0 and info.sound or 0x1FA)
    effect.on(caster, EFFECT, { speed = 10, duration = EFFECT_DURATION })
end
