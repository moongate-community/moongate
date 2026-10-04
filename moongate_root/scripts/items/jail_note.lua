-- ==============================================================================
-- Moongate - scripts/items/jail_note.lua
--
-- What it is for:
--   The item script of the note a prisoner finds in its backpack when its
--   jail sentence ends (item template jail_release_note): a double click shows
--   what the jail wrote on it, the days served, the cell, the dates and the
--   fine paid.
--
-- Props:
--   jail.text   the text of the note, written by the jail in the server's
--               language
--   jail.cell   the number of the cell
--   jail.days   the days served
--   jail.fine   the gold taken as a fine
--
-- Functions:
--   on_use(serial, user)   a player double clicks the note
-- ==============================================================================

jail_note = {}

function jail_note.on_use(serial, user)
    local text = item.get_prop(serial, "jail.text")

    -- A note made by hand, with .add, has nothing written on it.
    if not text then
        return
    end

    -- Not the name of this script: a gump answers to the script table of its own id.
    local g = gump.create("jail_release_note", 120, 120)
    g:background{ x = 0, y = 0, gump = 9380, width = 340, height = 240 }
    g:html{ x = 40, y = 45, width = 260, height = 150, text = text }
    gump.send(user, g, {})
end
