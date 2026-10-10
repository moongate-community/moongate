-- ==============================================================================
-- Moongate - scripts/items/magic_field.lua
--
-- What it is for:
--   The item script of the pieces of the fields of Magery (templates/items/
--   magic/fields.toml, set down by common/field.lua): a piece goes away when
--   its time is up, and a piece of fire burns whoever steps onto it or stands
--   in it, once a second, as the classic Fire Field. The caster is the
--   aggressor of whoever burns, as for a blow: an innocent that burns makes
--   the caster a criminal, and an NPC that burns fights back. Whoever cannot be
--   harmed (an invulnerable, a dead one) is left alone. A burn is lessened to
--   one point when the burned resists, by a try of Resisting Spells.
--
--   A piece of poison poisons, and a piece of paralysis paralyzes, whoever steps
--   onto it (a piece of poison also whoever stands in it, once a second), as the
--   classic Poison Field and Paralyze Field. These two do not touch a player
--   who looks innocent, unless the caster is a murderer, nor the caster's own
--   creatures (magic.valid_indirect); the caster itself is touched. They do
--   nothing once their caster has left the game.
--
-- Props it reads:
--   field.caster   the serial of who raised the field
--   field.until    the time, as world.now(), the piece goes away at
--   field.damage   the damage of a burn; none for a piece with no fire
--   field.effect   "poison" or "paralyze"; none for fire, a wall and energy
--   field.power    the poison level, or the seconds of paralysis
--
-- Functions:
--   on_move_over(serial, who)       a player stepped onto the piece
--   on_npc_move_over(serial, who)   an NPC stepped onto it
--   on_timer(serial, name)          the piece wakes ("tick") or ends ("expire")
--   burned_count()                  how many mobiles the list of the burned holds
-- ==============================================================================

local magic = require("common.magic")

magic_field = {}

local BURN_SOUND = 0x208
local POISON_SOUND = 0x474
local PARALYZE_SOUND = 0x204
local PARALYZE_EFFECT = 0x376A
local PARALYZE_SPEED = 10
local PARALYZE_DURATION = 16
local RESISTING = 501783       -- You feel yourself resisting magical energy.
local RESIST_MAX = 30          -- the Resisting Spells points at which the burn is always lessened
local HEIGHT_ABOVE = 16        -- a mobile stands in the field when its z is within these of the piece
local HEIGHT_UNDER = 12

-- When each mobile was burned last, in seconds: at most once a second. Only who was burned in the second that is
-- running is kept: whoever was burned before can be burned again at once.
local burned = {}
local swept = 0

-- Whether the mobile stands within reach, in height, of the piece.
local function inside(serial, who)
    local at = item.location(serial)
    local there = mobile.location(who)

    if not at or not there or there.map ~= at.map then
        return false
    end

    return not (there.z + HEIGHT_ABOVE <= at.z or at.z + HEIGHT_UNDER <= there.z)
end

-- Whether the mobile may be affected now: at most once a second, and the list is swept as time goes on.
local function once_a_second(who)
    local now = world.now()

    if swept ~= now then
        swept = now

        for key, at in pairs(burned) do
            if at < now then
                burned[key] = nil
            end
        end
    end

    if burned[who] == now then
        return false
    end

    burned[who] = now

    return true
end

local function burn(serial, who)
    local damage = item.get_prop(serial, "field.damage") or 0

    if damage <= 0 or mobile.is_dead(who) or not inside(serial, who) or not once_a_second(who) then
        return
    end

    local caster = item.get_prop(serial, "field.caster")

    if skill.check(who, "resisting_spells", 0, RESIST_MAX) then
        damage = 1
        mobile.message_cliloc(who, RESISTING)
    end

    mobile.play_sound(who, BURN_SOUND)

    -- A blow of the caster; one that is gone (left the game) leaves the fire to burn with no one to blame.
    if not (caster and combat.harm(who, damage, caster)) and not (caster and mobile.location(caster)) then
        combat.harm(who, damage)
    end
end

-- Whether the piece may do its harm to the mobile: its caster is in the game and the mobile is a valid target of a
-- spell of a place, which the caster then is the aggressor of.
local function harmed_by(serial, who)
    local caster = item.get_prop(serial, "field.caster")

    if not caster or not mobile.location(caster) or mobile.is_dead(who) or not magic.valid_indirect(caster, who) then
        return nil
    end

    if who ~= caster and not combat.aggress(caster, who) then
        return nil
    end

    return caster
end

local function poison(serial, who)
    if not inside(serial, who) or not once_a_second(who) or not harmed_by(serial, who) then
        return
    end

    mobile.poison(who, item.get_prop(serial, "field.power") or 1)
    mobile.play_sound(who, POISON_SOUND)
end

local function paralyze(serial, who)
    if not inside(serial, who) or not harmed_by(serial, who) then
        return
    end

    if mobile.paralyze(who, item.get_prop(serial, "field.power") or 1) then
        -- A paralysis ruins the spell its target is casting.
        spell.disturb(who)
        mobile.play_sound(who, PARALYZE_SOUND)
        effect.on(who, PARALYZE_EFFECT, { speed = PARALYZE_SPEED, duration = PARALYZE_DURATION })
    end
end

-- What a piece does to a mobile that is in it or steps onto it.
local function affect(serial, who, stepped)
    local kind = item.get_prop(serial, "field.effect")

    if kind == "poison" then
        poison(serial, who)
    elseif kind == "paralyze" then
        -- A paralysis is for who steps onto the piece, as the classic field: not for who is put down on it.
        if stepped then
            paralyze(serial, who)
        end
    else
        burn(serial, who)
    end
end

local function burn_all(serial)
    local at = item.location(serial)

    if not at then
        return
    end

    for _, who in ipairs(world.mobiles_in_range(at.map, at.x, at.y, 0)) do
        affect(serial, who, false)
    end
end

function magic_field.on_move_over(serial, who)
    affect(serial, who, true)
end

function magic_field.on_npc_move_over(serial, who)
    affect(serial, who, true)
end

function magic_field.on_timer(serial, name)
    local ends = item.get_prop(serial, "field.until") or 0

    if name == "expire" or world.now() >= ends then
        item.delete(serial)

        return
    end

    burn_all(serial)
    item.start_timer(serial, "tick", 1)
end

function magic_field.burned_count()
    local count = 0

    for _ in pairs(burned) do
        count = count + 1
    end

    return count
end
