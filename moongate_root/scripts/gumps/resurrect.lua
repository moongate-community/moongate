-- ==============================================================================
-- Moongate - scripts/gumps/resurrect.lua
--
-- What it is for:
--   The script of the question of an ankh (templates/gumps/resurrect.xml): the
--   Continue button brings the ghost back to life, as ModernUO's ResurrectGump
--   does, with the sound and the sparkles of a resurrection. The ghost may have
--   walked away, or been raised by someone else, while the gump was open.
--
-- Functions:
--   accept(player, response, args)  the Continue button; args.ankh is the ankh
--   cancel(player, response, args)   the Cancel button: nothing happens
-- ==============================================================================

resurrect = {}

-- How far from the ankh the ghost may stand, in cells.
local use_range = 2

-- Client text: "That is too far away."
local too_far_cliloc = 500446

local sound = 0x214

function resurrect.accept(player, response, args)
    if not mobile.is_dead(player) then
        return
    end

    if not item.in_range(args.ankh, player, use_range) then
        mobile.message_cliloc(player, too_far_cliloc)

        return
    end

    if mobile.resurrect(player) then
        mobile.play_sound(player, sound)
        effect.on(player, EffectGraphicType.SparkleHeal)
    end
end

function resurrect.cancel(player, response, args)
end
