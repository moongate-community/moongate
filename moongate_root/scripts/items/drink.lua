-- ==============================================================================
-- Moongate - scripts/items/drink.lua
--
-- What it is for:
--   The item script of what can be drunk, as ModernUO's beverages: double
--   clicking it drinks a sip. The player gets less thirsty (mobile.thirst, from
--   0 to 20), makes the sound and the gesture of drinking and is told so. A
--   player that is quenched is told so and drinks nothing. A container holds
--   some sips: a pitcher or a bottle 5, a jug 10, a glass or a mug 1. Once
--   empty, a pitcher or a glass turns into its empty graphic and stays; a
--   bottle or a jug is gone. An item template uses it with script_id = "drink".
--
-- Props it reads:
--   drink.fill   how much a sip quenches, from 1 up; 3 without it
--   drink.uses   the sips left; without it the container is full
--
-- Props it writes:
--   drink.uses   the sips left after a sip
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the drink, carried or
--                          on the ground within 2 tiles; returns true
-- ==============================================================================

drink = {}

-- How quenched a mobile can be, and how much a sip gives when its item does not say.
local FULL = 20
local FILL = 3

local SOUND = 0x30

-- The texts of the script, in data/messages.
local TOO_FULL = { id = 30130, english = "You are simply too full to drink any more!" }
local EMPTY = { id = 30131, english = "It is empty." }
local DRUNK = { id = 30132, english = "You drink, and feel less thirsty." }

local EMPTY_PITCHER = 0x0FF6
local FIRST_EMPTY_GLASS = 0x1F81

-- The containers by graphic: the sips a full one holds and what it is once empty (nothing: it is gone).
local function container_of(graphic)
    -- Pitchers of ale, cider, liquor, wine and water, then those of milk and the other two of water.
    if (graphic >= 0x1F95 and graphic <= 0x1F9E) or graphic == 0x09F0 or graphic == 0x09AD
        or graphic == 0x0FF8 or graphic == 0x0FF9 then
        return { sips = 5, empty = EMPTY_PITCHER, name = "empty pitcher" }
    end

    -- Glasses, four facings a drink: the empty glass keeps the facing.
    if graphic >= 0x1F7D and graphic <= 0x1F94 then
        return { sips = 1, empty = FIRST_EMPTY_GLASS + (graphic - 0x1F7D) % 4, name = "empty glass" }
    end

    -- Mugs of ale.
    if graphic == 0x09EE or graphic == 0x09EF then
        return { sips = 1, empty = FIRST_EMPTY_GLASS, name = "empty glass" }
    end

    -- Jugs of cider.
    if graphic == 0x09C8 or graphic == 0x098D or graphic == 0x098E then
        return { sips = 10 }
    end

    -- Bottles of liquor, ale and wine.
    if (graphic >= 0x099B and graphic <= 0x09A2) or (graphic >= 0x09C4 and graphic <= 0x09C7) then
        return { sips = 5 }
    end

    return { sips = 1 }
end

local function tell(user, text)
    mobile.message(user, localization.text(text.id) or text.english)
end

local function fill_of(serial)
    local fill = math.floor(tonumber(item.get_prop(serial, "drink.fill")) or FILL)

    return math.max(fill, 1)
end

-- Called when a player double clicks the drink.
function drink.on_use(serial, user)
    local thirst = mobile.thirst(user)

    if thirst == nil then
        return true
    end

    local container = container_of(item.item_id(serial) or 0)
    local uses = math.floor(tonumber(item.get_prop(serial, "drink.uses")) or container.sips)

    if uses <= 0 then
        tell(user, EMPTY)

        return true
    end

    if thirst >= FULL then
        tell(user, TOO_FULL)

        return true
    end

    local fill = fill_of(serial)
    uses = uses - 1

    if uses > 0 then
        item.set_prop(serial, "drink.uses", uses)
    elseif container.empty then
        item.set_prop(serial, "drink.uses", 0)
        item.set_item_id(serial, container.empty)
        item.set_name(serial, container.name)
    elseif not item.consume(serial, 1) then
        -- Held on a cursor, or gone in the meantime: nothing is drunk.
        return true
    end

    mobile.play_sound(user, SOUND)

    -- Only a human body, of any race, has the gesture of drinking.
    if mobile.body_type(user) == BodyType.Human then
        mobile.animate(user, HumanAnimationType.Eat)
    end

    mobile.set_thirst(user, math.min(thirst + fill, FULL))
    tell(user, DRUNK)

    return true
end
