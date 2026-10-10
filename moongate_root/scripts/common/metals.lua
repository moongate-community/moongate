-- ==============================================================================
-- Moongate - scripts/common/metals.lua
--
-- What it is for:
--   The metals, shared by the scripts that dig, smelt and forge them: for each
--   its ore and its ingots, the Mining it asks for to dig it, the bounds a dig
--   of it is tried between, the difficulty of smelting it and the text of its
--   ore. A place's metal is the vein of the resource "ore" of
--   data/harvest.toml. Used with local metals = require("common.metals").
--
-- Functions:
--   metals.by_id(id)             the metal of that id ("iron", "dull_copper", ...), or nil
--   metals.of_ore(template)      the metal of a template of ore, or nil
--   metals.of_ingot(template)    the metal of a template of ingots, or nil
--
-- Tables:
--   metals.iron     iron, whose ore comes in four piles
--   metals.kinds    the other metals, from the commonest to the rarest
-- ==============================================================================

local metals = {}

metals.iron = {
    id = "iron", name = "iron", ingot = "0x1bf2_iron_ingot",
    -- The four piles of iron ore, by their graphic; a dig gives one of them.
    ores = { "0x19b7_iron_ore", "0x19b8_iron_ore", "0x19b9_iron_ore", "0x19ba_iron_ore" },
    other_ingot = "0x1bef_iron_ingot",
    mining = 0, min = 0, max = 100, smelt = 50, dug = 1007072,
}

metals.kinds = {
    { id = "dull_copper", name = "dull copper", ore = "ore_dull_copper", ingot = "ingot_dull_copper", mining = 65, min = 25, max = 105, smelt = 65, dug = 1007073 },
    { id = "shadow_iron", name = "shadow iron", ore = "ore_shadow_iron", ingot = "ingot_shadow_iron", mining = 70, min = 30, max = 110, smelt = 70, dug = 1007074 },
    { id = "copper", name = "copper", ore = "ore_copper", ingot = "ingot_copper", mining = 75, min = 35, max = 115, smelt = 75, dug = 1007075 },
    { id = "bronze", name = "bronze", ore = "ore_bronze", ingot = "ingot_bronze", mining = 80, min = 40, max = 120, smelt = 80, dug = 1007076 },
    { id = "gold", name = "gold", ore = "ore_gold", ingot = "ingot_gold", mining = 85, min = 45, max = 125, smelt = 85, dug = 1007077 },
    { id = "agapite", name = "agapite", ore = "ore_agapite", ingot = "ingot_agapite", mining = 90, min = 50, max = 130, smelt = 90, dug = 1007078 },
    { id = "verite", name = "verite", ore = "ore_verite", ingot = "ingot_verite", mining = 95, min = 55, max = 135, smelt = 95, dug = 1007079 },
    { id = "valorite", name = "valorite", ore = "ore_valorite", ingot = "ingot_valorite", mining = 99, min = 59, max = 139, smelt = 99, dug = 1007080 },
}

local by_id = { iron = metals.iron }
local by_ore = {}
local by_ingot = { [metals.iron.ingot] = metals.iron, [metals.iron.other_ingot] = metals.iron }

for _, template in ipairs(metals.iron.ores) do
    by_ore[template] = metals.iron
end

for _, kind in ipairs(metals.kinds) do
    by_id[kind.id] = kind
    by_ore[kind.ore] = kind
    by_ingot[kind.ingot] = kind
end

function metals.by_id(id)
    return id and by_id[id]
end

function metals.of_ore(template)
    return template and by_ore[template]
end

function metals.of_ingot(template)
    return template and by_ingot[template]
end

return metals
