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
-- Props it reads:
--   field.caster   the serial of who raised the field
--   field.until    the time, as world.now(), the piece goes away at
--   field.damage   the damage of a burn; none for a piece with no fire
--
-- Functions:
--   on_move_over(serial, who)       a player stepped onto the piece
--   on_npc_move_over(serial, who)   an NPC stepped onto it
--   on_timer(serial, name)          the piece wakes ("tick") or ends ("expire")
-- ==============================================================================

magic_field = {}

local BURN_SOUND = 0x208
local RESISTING = 501783       -- You feel yourself resisting magical energy.
local RESIST_MAX = 30          -- the Resisting Spells points at which the burn is always lessened
local HEIGHT_ABOVE = 16        -- a mobile stands in the field when its z is within these of the piece
local HEIGHT_UNDER = 12

-- When each mobile was burned last, in seconds: at most once a second.
local burned = {}

local function burn(serial, who)
    local damage = item.get_prop(serial, "field.damage") or 0

    if damage <= 0 or mobile.is_dead(who) then
        return
    end

    local at = item.location(serial)
    local there = mobile.location(who)

    if not at or not there or there.map ~= at.map then
        return
    end

    if there.z + HEIGHT_ABOVE <= at.z or at.z + HEIGHT_UNDER <= there.z then
        return
    end

    local now = world.now()

    if burned[who] == now then
        return
    end

    burned[who] = now

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

local function burn_all(serial)
    local at = item.location(serial)

    if not at then
        return
    end

    for _, who in ipairs(world.mobiles_in_range(at.map, at.x, at.y, 0)) do
        burn(serial, who)
    end
end

function magic_field.on_move_over(serial, who)
    burn(serial, who)
end

function magic_field.on_npc_move_over(serial, who)
    burn(serial, who)
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
