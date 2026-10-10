-- ==============================================================================
-- Moongate - scripts/skills/animal_lore.lua
--
-- What it is for:
--   The Animal Lore skill, as ModernUO's and ServUO's: the player picks a creature within 8 tiles and, if the skill check
--   from 0 to 120 passes, reads a two-page gump about it: its hits, stamina and mana, strength, dexterity and
--   intelligence, armor, damage, loyalty, combat and lore skills, the food it eats and the Animal Taming it asks. What
--   the gump shows does not depend on the skill; the skill decides which creatures may be looked at: a tamed one always,
--   a tameable one from 100 points, any other from 110.
--
--   The refusals and the failure are the client's own texts. A pick is waited 1 second after, as the other lore skills.
--
-- Functions:
--   on_use(user)   the player user uses the skill; returns the seconds before the next skill, 1
-- ==============================================================================

animal_lore = {}

-- Seconds before the next skill, how far the creature may be, and the window of the roll, in points.
local DELAY = 1
local SIGHT = 8
local ROLL_FROM = 0
local ROLL_TO = 120

-- Skill points from which a creature that is not tamed may be looked at: a tameable one, any other.
local TAMEABLE_FROM = 100
local ANY_FROM = 110

-- The client's texts.
local WHICH = 500328       -- What animal should I look at?
local NOT_AN_ANIMAL = 500329 -- That's not an animal!
local TOO_FAR = 500446     -- That is too far away.
local NO_SIGHT = 1049654   -- You can no longer see the creature.
local FAILED = 500334      -- You can't think of anything you know offhand.
local ONLY_TAMED = 1049674 -- At your skill level, you can only lore tamed creatures.
local ONLY_TAMEABLE = 1049675 -- At your skill level, you can only lore tamed or tameable creatures.

-- The labels of the gump, the client's own, in its language.
local ATTRIBUTES = 1049593
local HITS, STAMINA, MANA = 1049578, 1049579, 1049580
local STRENGTH, DEXTERITY, INTELLIGENCE = 1028335, 3000113, 3000112
local MISCELLANEOUS = 3001016
local ARMOR_RATING = 1049581
local BASE_DAMAGE = 1076750
local LOYALTY_RATING = 1049594
local WILD = 1061643
local LOYALTY_FIRST = 1049595 -- plus the loyalty divided by ten: confused ... wonderfully happy
local COMBAT_RATINGS = 3001030
local LORE_AND_KNOWLEDGE = 3001032
local PREFERRED_FOODS = 1049563
local NO_FOOD = 3000340
local TAMING_SKILL = 1044095 -- Animal Taming
local BONDED = 1049608 -- (bonded)
local TAME = 502006 -- (tame)

-- The skills it shows, by name, with their label.
local COMBAT_SKILLS = {
    { "wrestling", 1044103 },
    { "tactics", 1044087 },
    { "resisting_spells", 1044086 },
    { "anatomy", 1044061 },
}
local LORE_SKILLS = {
    { "magery", 1044085 },
    { "evaluating_intelligence", 1044076 },
    { "meditation", 1044106 },
}

-- The kinds of food a creature eats, as taming.toml names them, with their label.
local FOODS = {
    meat = 1049564,
    fruit = 1049565,
    grain = 1049566,
    fish = 1049568,
    eggs = 1044477,
}

-- The layout of the gump: where the labels and the values are, and the height of a line.
local LABEL_X = 40
local VALUE_X = 235
local LINE = 20
local LABEL_HUE = 0x24E5
local HEADER_HUE = 200

-- Shown instead of a value the creature does not have.
local NONE = "---"

local function distance(a, b)
    return math.max(math.abs(a.x - b.x), math.abs(a.y - b.y))
end

-- Why the creature may not be looked at, as the client says it, or nil. lore is the skill of the player in points.
local function refusal(creature, lore, points)
    local body = mobile.body_type(creature)

    if lore == nil or (body ~= BodyType.Animal and body ~= BodyType.Monster and body ~= BodyType.Sea) then
        return NOT_AN_ANIMAL
    end

    local tamed = lore.owner ~= 0
    local tameable = lore.min_skill ~= nil

    if not tamed and points < TAMEABLE_FROM then
        return ONLY_TAMED
    end

    if not tamed and not tameable and points < ANY_FROM then
        return ONLY_TAMEABLE
    end

    return nil
end

-- A number of the creature, or --- when it has none.
local function amount(value)
    if value == nil or value <= 0 then
        return NONE
    end

    return value
end

local function bar(current, maximum)
    if maximum == nil or maximum <= 0 then
        return NONE
    end

    return current .. "/" .. maximum
end

-- A line of the gump: its label, a client text, and its value on the right.
local function line(g, y, label, value)
    g:html{ x = LABEL_X, y = y, width = 190, height = LINE, cliloc = label, color = LABEL_HUE }
    g:text{ x = VALUE_X, y = y, hue = HEADER_HUE, text = tostring(value) }
end

local function header(g, y, label)
    g:html{ x = LABEL_X - 10, y = y, width = 250, height = LINE, cliloc = label, color = HEADER_HUE }
end

local function skills_block(g, y, creature, label, list)
    local skills = mobile.skills(creature) or {}

    header(g, y, label)

    for _, entry in ipairs(list) do
        y = y + LINE

        local points = skills[entry[1]] or 0
        line(g, y, entry[2], points >= 10 and string.format("%.1f", points) or NONE)
    end

    return y + LINE
end

local function loyalty_label(lore)
    if lore.owner == 0 or lore.loyalty == nil or lore.loyalty <= 0 then
        return WILD
    end

    return LOYALTY_FIRST + math.floor(lore.loyalty / 10)
end

-- The first page: what the creature is.
local function first_page(g, creature, lore)
    local stats = mobile.stats(creature)
    local y = 60

    header(g, y, ATTRIBUTES)
    line(g, y + LINE, HITS, bar(stats.hits, stats.hits_max))
    line(g, y + 2 * LINE, STAMINA, bar(stats.stamina, stats.stamina_max))
    line(g, y + 3 * LINE, MANA, bar(stats.mana, stats.mana_max))
    line(g, y + 4 * LINE, STRENGTH, amount(stats.strength))
    line(g, y + 5 * LINE, DEXTERITY, amount(stats.dexterity))
    line(g, y + 6 * LINE, INTELLIGENCE, amount(stats.intelligence))

    y = y + 8 * LINE
    header(g, y, MISCELLANEOUS)
    line(g, y + LINE, ARMOR_RATING, amount(lore.armor))
    line(g, y + 2 * LINE, BASE_DAMAGE, lore.damage_max > 0 and (lore.damage_min .. "-" .. lore.damage_max) or NONE)

    y = y + 4 * LINE
    header(g, y, LOYALTY_RATING)
    g:html{ x = LABEL_X, y = y + LINE, width = 240, height = LINE, cliloc = loyalty_label(lore), color = LABEL_HUE }
end

-- The second page: what it can do and what it eats.
local function second_page(g, creature, lore)
    local y = skills_block(g, 60, creature, COMBAT_RATINGS, COMBAT_SKILLS)
    y = skills_block(g, y + LINE, creature, LORE_AND_KNOWLEDGE, LORE_SKILLS)

    y = y + LINE
    header(g, y, PREFERRED_FOODS)

    if #lore.foods == 0 then
        g:html{ x = LABEL_X, y = y + LINE, width = 240, height = LINE, cliloc = NO_FOOD, color = LABEL_HUE }
        y = y + 2 * LINE
    else
        for _, kind in ipairs(lore.foods) do
            y = y + LINE
            g:html{ x = LABEL_X, y = y, width = 240, height = LINE, cliloc = FOODS[kind] or NO_FOOD, color = LABEL_HUE }
        end

        y = y + LINE
    end

    if lore.min_skill ~= nil then
        line(g, y + LINE, TAMING_SKILL, string.format("%.1f", lore.min_skill))
    end
end

local function show(user, creature, lore)
    local g = gump.create("animal_lore", 250, 50)

    g:background{ x = 0, y = 0, gump = 9200, width = 340, height = 470 }
    g:text{ x = 40, y = 25, hue = HEADER_HUE, text = npc.name(creature) or "" }
    if lore.owner ~= 0 then
        g:html{ x = 200, y = 25, width = 100, height = LINE, cliloc = lore.bonded and BONDED or TAME, color = LABEL_HUE }
    end

    g:page()
    first_page(g, creature, lore)
    g:button{ x = 290, y = 435, up = 5601, down = 5603, page = 2 }
    g:page()
    second_page(g, creature, lore)
    g:button{ x = 20, y = 435, up = 5603, down = 5601, page = 1 }

    gump.send(user, g, {})
end

-- Why the creature cannot be reached: too far, or out of sight; nil when it can be looked at.
local function unreachable(user, creature)
    local at, there = mobile.location(user), mobile.location(creature)

    if at == nil or there == nil or at.map ~= there.map or distance(at, there) > SIGHT then
        return TOO_FAR
    end

    if not world.line_of_sight(at.map, at.x, at.y, at.z, there.x, there.y, there.z) then
        return NO_SIGHT
    end

    return nil
end

local function look(user, creature)
    -- The reach is the cursor's, so it comes first, as in ModernUO.
    local far = unreachable(user, creature)

    if far ~= nil then
        mobile.message_cliloc(user, far)

        return
    end

    local points = (mobile.skills(user) or {}).animal_lore or 0
    local lore = pet.lore(creature)
    local why = refusal(creature, lore, points)

    if why ~= nil then
        mobile.message_cliloc(user, why)

        return
    end

    if not skill.check(user, "animal_lore", ROLL_FROM, ROLL_TO) then
        mobile.message_cliloc(user, FAILED)

        return
    end

    show(user, creature, lore)
end

function animal_lore.on_use(user)
    mobile.message_cliloc(user, WHICH)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        look(user, picked.serial)
    end)

    return DELAY
end
