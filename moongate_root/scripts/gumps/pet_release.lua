-- ==============================================================================
-- Moongate - scripts/gumps/pet_release.lua
--
-- What it is for:
--   The script of the question of the word "release" (templates/gumps/pet_release.xml): Release lets the pet go, as the
--   pet module does; Keep does nothing. The pet may have been tamed again, stabled or killed while the question was
--   open: then nothing happens.
--
-- Functions:
--   yes(player, response, args)  the Release button; args.pet is the creature
--   no(player, response, args)   the Keep button
-- ==============================================================================

pet_release = {}

-- How far the pet may be, in tiles, as the words are heard.
local reach = 14

-- Client text: "That is too far away."
local too_far_cliloc = 500446

function pet_release.yes(player, response, args)
    if mobile.is_dead(player) or npc.get_prop(args.pet, "owner") ~= player then
        return
    end

    local here, there = mobile.location(player), npc.location(args.pet)

    if here == nil or there == nil or here.map ~= there.map
        or math.max(math.abs(here.x - there.x), math.abs(here.y - there.y)) > reach then
        mobile.message_cliloc(player, too_far_cliloc)

        return
    end

    pet.release(player, args.pet)
end

function pet_release.no(player, response, args)
end
