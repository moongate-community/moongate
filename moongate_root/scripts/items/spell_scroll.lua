-- ==============================================================================
-- Moongate - scripts/items/spell_scroll.lua
--
-- What it is for:
--   The item script of the scrolls of the spells, with script_id =
--   "spell_scroll" (the scroll templates in templates/items/magic/scrolls.toml):
--   a double click casts the spell of the scroll, which must lie in the
--   backpack. It asks no reagents and a skill window two circles easier than the
--   book's, and one scroll of the stack is used up when the spell succeeds; a
--   spell that fizzles keeps it. A scroll dropped on a spellbook is written in
--   it instead: the server does that.
--
-- Functions:
--   spell_scroll.on_use(serial, user)   begins the cast; it returns true
-- ==============================================================================

spell_scroll = {}

function spell_scroll.on_use(serial, user)
    spell.cast_scroll(user, serial)

    return true
end
