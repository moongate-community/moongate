local function add_backpack(loadout, template_id, amount, args)
    table.insert(loadout.backpack, {
        template_id = template_id,
        amount = amount,
        args = args
    })
end

local function add_equip(loadout, template_id, layer, args)
    table.insert(loadout.equip, {
        template_id = template_id,
        layer = layer,
        args = args
    })
end

function build_starting_loadout(context)
    local profession = string.lower(context.profession or "")
    local race = string.lower(context.race or "human")

    local loadout = {
        backpack = {},
        equip = {}
    }

    add_backpack(loadout, "RedBook", 1, {
        title = "a book",
        author = context.player_name,
        pages = 20,
        writable = true
    })
    add_backpack(loadout, "Gold", 1000)
    add_backpack(loadout, "Dagger", 1)
    add_backpack(loadout, "Candle", 1)

    add_equip(loadout, "Shirt", "Shirt")
    add_equip(loadout, "Pants", "Pants")
    add_equip(loadout, "Shoes", "Shoes")
    add_equip(loadout, "BankBox", "Bank")

    if race == "elf" then
        add_backpack(loadout, "WildStaff", 1)
    elseif race == "gargoyle" then
        add_backpack(loadout, "GargishDagger", 1)
    end

    if profession == "warrior" then
        add_backpack(loadout, "Broadsword", 1)
    elseif profession == "mage" then
        add_backpack(loadout, "Spellbook", 1)
    elseif profession == "blacksmith" then
        add_backpack(loadout, "Tongs", 1)
        add_backpack(loadout, "IronIngot", 50)
    end

    return loadout
end
