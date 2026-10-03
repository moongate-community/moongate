-- ==============================================================================
-- Moongate - scripts/items/fillable.lua
--
-- What it is for:
--   The item script of the town containers that fill up (item template
--   decoration_fillable), as ModernUO's FillableContainer: the crates, boxes,
--   chests and barrels of the shops and the bookcases of the libraries. When a
--   player opens one that holds few items and whose time has come, it gets a
--   few more from the loot table of its kind (templates/loots/
--   fillable_containers.toml), then waits 60 to 90 minutes. Nothing runs while
--   nobody opens it.
--
--   The kind is the prop content_type, such as "baker" for the table
--   fillable_baker: ".decorate" sets it where the decoration names one, and a
--   bookcase is a "library". Without it the container takes the kind of the
--   nearest vendor within 20 tiles and keeps it; with no vendor around it
--   stays empty and looks again at the next opening.
--
-- Props:
--   content_type   the kind: the table is fillable_<content_type>
--   fill.next      when it may fill again, in seconds as world.now() gives
--
-- Functions:
--   on_use(serial, user)   a player double clicks the container; it returns
--                          nothing, so the container opens after it
-- ==============================================================================

fillable = {}

-- A container with more items than this is full enough.
local threshold = 2

-- A bookcase fills up to this many books.
local library_size = 5

local vendor_range = 20

-- Seconds between two fills.
local min_wait = 60 * 60
local max_wait = 90 * 60

-- Mobile template -> kind, as the vendors of ModernUO's tables. The female and
-- male templates (f_baker, m_baker) count as their trade.
local VENDORS = {
    alchemist = "alchemist",
    armourer = "armorer",
    baker = "baker",
    bard = "bard",
    blacksmith = "blacksmith",
    bowyer = "bowyer",
    butcher = "butcher",
    carpenter = "carpenter",
    architect = "carpenter",
    tailor = "clothier",
    weaver = "clothier",
    cobbler = "cobbler",
    fisher = "docks",
    farmer = "farm",
    rancher = "farm",
    healer = "healer",
    herbalist = "herbalist",
    innkeeper = "inn",
    jeweler = "jeweler",
    scribe = "library",
    mage = "mage",
    miner = "mine",
    provisioner = "provisioner",
    ranger = "ranger",
    animaltrainer = "stables",
    gypsyanimaltrainer = "stables",
    tanner = "tanner",
    leatherworker = "tanner",
    furtrader = "tanner",
    tavernkeeper = "tavern",
    waiter = "tavern",
    waitress = "tavern",
    cook = "tavern",
    thief = "thief_guild",
    thief_guildmaster = "thief_guild",
    tinker = "tinker",
    veterinarian = "veterinarian",
    weaponsmith = "weaponsmith",
}

-- The kind of the nearest vendor around the container; nil when there is none.
local function nearest_vendor(serial)
    local at = item.location(serial)

    if not at then
        return nil
    end

    local kind, nearest

    for _, who in ipairs(world.mobiles_in_range(at.map, at.x, at.y, vendor_range)) do
        local template = mobile.template(who)
        local trade = template and VENDORS[(template:gsub("^[fm]_", ""))]
        local where = trade and mobile.location(who)

        if where then
            local distance = math.max(math.abs(where.x - at.x), math.abs(where.y - at.y))

            if not nearest or distance < nearest then
                kind, nearest = trade, distance
            end
        end
    end

    return kind
end

-- The kind of the container: its own, else that of the nearest vendor, kept from then on.
local function kind_of(serial)
    local kind = item.get_prop(serial, "content_type")

    if not kind then
        kind = nearest_vendor(serial)

        if kind then
            item.set_prop(serial, "content_type", kind)
        end
    end

    return kind
end

-- How many items a fill adds to a container that holds count of them, as ModernUO: a bookcase one book up to its
-- size, the others none up to twice what they miss to pass the threshold.
local function share(kind, count)
    if kind == "library" then
        return count < library_size and math.random(1, library_size - count) or 0
    end

    return math.random(0, (1 + threshold - count) * 2)
end

-- Called when a player double clicks the container, before it opens.
function fillable.on_use(serial, user)
    local now = world.now()

    if now < (item.get_prop(serial, "fill.next") or 0) then
        return
    end

    local count = #item.contents(serial)
    local kind = kind_of(serial)

    -- Full enough, or of no kind yet: looked at again at the next opening.
    if not kind or (kind == "library" and count >= library_size) or (kind ~= "library" and count > threshold) then
        return
    end

    local wanted = share(kind, count)
    local added = 0

    for _ = 1, wanted do
        added = added + item.add_loot(serial, "fillable_" .. kind)
    end

    -- Rolls that added nothing, such as when the server has no item serial at hand: tried again at the next opening.
    if wanted > 0 and added == 0 then
        return
    end

    item.set_prop(serial, "fill.next", now + math.random(min_wait, max_wait))
end
