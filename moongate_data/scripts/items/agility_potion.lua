require("items.potion_common")

items_agility_potion = {
    on_double_click = function(ctx)
        local consumable, mobile_serial = potion_common.resolve(ctx)
        if consumable == nil or mobile_serial <= 0 then
            return
        end

        if potion_common.consume(consumable, ctx.item.amount) then
            potion_effects.apply_temporary_dexterity(mobile_serial, 10, 120000)
        end
    end,
}
