-- ==============================================================================
-- Moongate - scripts/common/woods.lua
--
-- What it is for:
--   The kinds of wood, shared by the scripts that chop, saw and work it: for
--   each kind its logs and its boards, the Lumberjacking it asks for to chop
--   it and to saw its logs, the Carpentry it asks for to make things of it,
--   the bounds a cut of it is tried between and the text of its logs. A
--   place's kind is the vein of the resource "wood" of data/harvest.toml.
--   Used with local woods = require("common.woods").
--
-- Functions:
--   woods.by_id(id)              the kind of that id ("plain", "oak", ...), or nil
--   woods.of_logs(template)      the kind of a template of logs, or nil
--   woods.of_boards(template)    the kind of a template of boards, or nil
--
-- Tables:
--   woods.plain    plain wood
--   woods.kinds    the other kinds, from the commonest to the rarest
-- ==============================================================================

local woods = {}

woods.plain = {
    id = "plain", name = "plain", logs = "0x1be0_log", boards = "0x1bd7_board",
    -- The other shapes of plain logs and boards, which shops sell: they count too.
    other_logs = "0x1bdd_log", other_boards = "0x1bda_board",
    lumberjacking = 0, carpentry = 0, min = 0, max = 100, chopped = 500498,
}

woods.kinds = {
    { id = "oak", name = "oak", logs = "oak_log", boards = "oak_board", lumberjacking = 65, carpentry = 65, min = 25, max = 105, chopped = 1072541 },
    { id = "ash", name = "ash", logs = "ash_log", boards = "ash_board", lumberjacking = 80, carpentry = 80, min = 40, max = 120, chopped = 1072542 },
    { id = "yew", name = "yew", logs = "yew_log", boards = "yew_board", lumberjacking = 95, carpentry = 95, min = 55, max = 135, chopped = 1072543 },
    { id = "heartwood", name = "heartwood", logs = "heartwood_log", boards = "heartwood_board", lumberjacking = 100, carpentry = 100, min = 60, max = 140, chopped = 1072544 },
    { id = "bloodwood", name = "bloodwood", logs = "bloodwood_log", boards = "bloodwood_board", lumberjacking = 100, carpentry = 100, min = 60, max = 140, chopped = 1072545 },
    { id = "frostwood", name = "frostwood", logs = "frostwood_log", boards = "frostwood_board", lumberjacking = 100, carpentry = 100, min = 60, max = 140, chopped = 1072546 },
}

local by_id = { plain = woods.plain }
local by_logs = { [woods.plain.logs] = woods.plain, [woods.plain.other_logs] = woods.plain }
local by_boards = { [woods.plain.boards] = woods.plain, [woods.plain.other_boards] = woods.plain }

for _, kind in ipairs(woods.kinds) do
    by_id[kind.id] = kind
    by_logs[kind.logs] = kind
    by_boards[kind.boards] = kind
end

function woods.by_id(id)
    return id and by_id[id]
end

function woods.of_logs(template)
    return template and by_logs[template]
end

function woods.of_boards(template)
    return template and by_boards[template]
end

return woods
