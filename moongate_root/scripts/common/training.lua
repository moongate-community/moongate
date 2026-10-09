-- ==============================================================================
-- Moongate - scripts/common/training.lua
--
-- What it is for:
--   What the scripts of the NPCs that teach skills share, as ModernUO's trainers:
--   the Train entries of the context menu, the word "train" that lists the skills
--   an NPC teaches, and the gold dropped on it to pay. An NPC teaches the skills
--   it has at 60.0 or more, up to a third of their value (42.0 at most). The
--   price is quoted by the server (the trainer module): 1 gold for each tenth of
--   a point, and for less gold it teaches less. Used with
--   local training = require("common.training").
--
-- Functions:
--   training.entries(serial, player, range)   the Train entries of the context menu
--   training.select(serial, player, id)       true when id was a Train entry, and
--                                             the price is quoted
--   training.listen(serial, speaker, keywords) the NPC lists its skills at the word
--                                             "train", within 4 cells
--   training.drop(serial, giver, item)        the answer of an on_drag_drop: true
--                                             when the gold paid for a lesson
-- ==============================================================================

local training = {}

-- How far an NPC hears "train", in cells.
local speech_range = 4

-- The client's texts.
local entry_base = 3006000    -- + the skill: the name of the skill, in the context menu
local list_intro = 1043058    -- I can train the following:
local list_base = 1043059     -- + the skill: the name of the skill
local list_last = 1043107
local nothing_to_teach = 501505  -- Alas, I cannot teach thee anything.

local prefix = "train:"

local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

function training.entries(serial, player, range)
    local entries = {}

    for _, skill in ipairs(trainer.skills(serial, player)) do
        entries[#entries + 1] = { id = prefix .. skill, cliloc = entry_base + skill, range = range }
    end

    return entries
end

function training.select(serial, player, id)
    if type(id) ~= "string" or id:sub(1, #prefix) ~= prefix then
        return false
    end

    local skill = tonumber(id:sub(#prefix + 1))

    if skill then
        npc.look_at(serial, player)
        trainer.quote(serial, player, skill)
    end

    return true
end

function training.listen(serial, speaker, keywords)
    if not has_keyword(keywords, SpeechKeywordType.Train) then
        return
    end

    -- A ghost is taught nothing, and gets no answer.
    if mobile.is_dead(speaker) then
        return
    end

    local here = npc.location(serial)
    local there = mobile.location(speaker)

    if not (here and there and here.map == there.map
        and math.abs(here.x - there.x) <= speech_range and math.abs(here.y - there.y) <= speech_range) then
        return
    end

    local listed = false

    for _, skill in ipairs(trainer.skills(serial, speaker)) do
        local number = list_base + skill

        if number <= list_last then
            if not listed then
                npc.look_at(serial, speaker)
                npc.say_cliloc(serial, list_intro)
                listed = true
            end

            npc.say_cliloc(serial, number)
        end
    end

    if not listed then
        npc.say_cliloc(serial, nothing_to_teach)
    end
end

function training.drop(serial, giver, item)
    return trainer.pay(serial, giver, item)
end

return training
