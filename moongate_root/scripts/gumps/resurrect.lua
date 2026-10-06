-- ==============================================================================
-- Moongate - scripts/gumps/resurrect.lua
--
-- What it is for:
--   The script of the question of an ankh or of a healer (templates/gumps/
--   resurrect.xml): the Continue button brings the ghost back to life, as
--   ModernUO's ResurrectGump does, with the sound and the sparkles of a
--   resurrection, and it costs a tenth of its fame. The ghost may have walked
--   away, or been raised by someone else, while the gump was open.
--
-- Functions:
--   accept(player, response, args)  the Continue button; args.ankh is the ankh,
--                                    args.healer the healer that offered
--   cancel(player, response, args)   the Cancel button: nothing happens
-- ==============================================================================

resurrect = {}

-- How far from the ankh the ghost may stand, in cells; a healer may be farther, as it came to the ghost.
local use_range = 2
local healer_range = 8

-- The fame lost by who comes back: a tenth of it, as ModernUO.
local fame_tenth = 10

-- Client text: "That is too far away."
local too_far_cliloc = 500446

local sound = 0x214

-- Whether the ankh or the healer that asked is still within reach of the ghost.
local function in_reach(player, args)
    if args.healer then
        local at = mobile.location(player)
        local distance = at and npc.distance_to(args.healer, at.x, at.y)

        return distance ~= nil and distance <= healer_range
    end

    return item.in_range(args.ankh, player, use_range)
end

function resurrect.accept(player, response, args)
    if not mobile.is_dead(player) then
        return
    end

    if not in_reach(player, args) then
        mobile.message_cliloc(player, too_far_cliloc)

        return
    end

    if mobile.resurrect(player) then
        mobile.play_sound(player, sound)
        effect.on(player, EffectGraphicType.SparkleHeal)

        local stats = mobile.stats(player)

        if stats and stats.fame > 0 then
            mobile.set_stats(player, { fame = stats.fame - math.floor(stats.fame / fame_tenth) })
        end
    end
end

function resurrect.cancel(player, response, args)
end
