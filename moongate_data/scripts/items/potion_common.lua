potion_common = {
    resolve = function(ctx)
        if ctx == nil or ctx.item == nil then
            return nil, 0
        end

        local item_serial = convert.to_int(ctx.item.serial, 0)
        if item_serial <= 0 then
            return nil, 0
        end

        local consumable = item.get(item_serial)
        if consumable == nil then
            return nil, 0
        end

        local mobile_serial = convert.to_int(ctx.mobile_id, 0)
        if mobile_serial <= 0 then
            return nil, 0
        end

        return consumable, mobile_serial
    end,

    consume = function(consumable, amount)
        if consumable == nil then
            return false
        end

        local current_amount = convert.to_int(amount, 0)
        if current_amount > 1 then
            return consumable:add_amount(-1)
        end

        return consumable:delete()
    end,
}
