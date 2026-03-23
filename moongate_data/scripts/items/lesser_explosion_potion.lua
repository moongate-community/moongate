require("items.potion_common")

items_lesser_explosion_potion = {
    on_double_click = function(ctx)
        local consumable = potion_common.resolve(ctx)
        if consumable == nil then
            return
        end

        potion_common.consume(consumable, ctx.item.amount)
    end,
}
