-- ==============================================================================
-- Moongate - scripts/items/bulletin_board.lua
--
-- What it is for:
--   The item script of the bulletin boards (the item template bulletin_board
--   that ".decorate" places, and 0x1e5e_bulletin_board and
--   0x1e5f_bulletin_board for a board added by hand): a player who double
--   clicks one within two tiles gets the board and the list of its messages,
--   and from there reads, posts, replies and removes its own. Every board has
--   its own messages. How long a thread lasts, how many messages a board holds
--   and how long a player waits between two posts are the settings of
--   [ultima.bulletin_boards].
--
-- Functions:
--   on_use(serial, user)   a player double clicks the board; returns true
-- ==============================================================================

bulletin_board = {}

function bulletin_board.on_use(serial, user)
    board.open(serial, user)

    return true
end
