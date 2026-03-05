items_door = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = tonumber(ctx.item.serial or 0)
        if serial == nil or serial == 0 then
            return
        end

        if not door.is_door(serial) then
            return
        end

        door.toggle(serial)
    end,
}
