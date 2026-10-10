-- ==============================================================================
-- Moongate - scripts/items/spellbook.lua
--
-- What it is for:
--   The item script of the spellbook, with script_id = "spellbook" (the
--   templates of the book in templates/items/magic/misc_magic.toml): a double
--   click opens the book, showing the spells it holds, when the player wears it
--   or carries it in its backpack; else it is told the book must be carried. A
--   scroll dropped on the book adds its spell: the server does that.
--
-- Functions:
--   spellbook.on_use(serial, user)   opens the book; it returns true
-- ==============================================================================

spellbook = {}

function spellbook.on_use(serial, user)
    spell.open_book(user, serial)

    return true
end
