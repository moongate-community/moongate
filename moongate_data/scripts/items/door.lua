items_door = {
    OPEN_SOUND = 0x00EA,
    CLOSE_SOUND = 0x00F1,

    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = convert.to_int(ctx.item.serial, 0)
        if serial == nil or serial == 0 then
            return
        end

        if not door.is_door(serial) then
            return
        end

        local toggled = door.toggle(serial, convert.to_int(ctx.session_id, 0))
        if not toggled then
            return
        end

        local item_proxy = item.get(serial)
        if item_proxy == nil then
            return
        end

        local item_id = convert.to_int(item_proxy.item_id, 0)
        if item_id <= 0 then
            return
        end

        local is_open_state = (item_id % 2) == 0
        local sound_id = is_open_state and items_door.OPEN_SOUND or items_door.CLOSE_SOUND
        item_proxy:play_sound(sound_id)
    end,
}
