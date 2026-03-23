items_beverage = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = convert.to_int(ctx.item.serial, 0)
        if serial <= 0 then
            return
        end

        local consumable = item.get(serial)
        if consumable == nil then
            return
        end

        local amount = convert.to_int(ctx.item.amount, 0)
        if amount > 1 then
            consumable:add_amount(-1)
            return
        end

        consumable:delete()
    end,
}
