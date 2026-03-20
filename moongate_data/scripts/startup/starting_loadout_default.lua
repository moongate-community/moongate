function build_starting_loadout(context)
    _ = context

    return {
        backpack = {
            {
                template_id = "Gold",
                amount = 1000
            }
        },
        equip = {}
    }
end
