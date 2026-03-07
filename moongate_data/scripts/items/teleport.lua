local TELEPORT_EFFECT_ITEM_ID = 0x3728

local function try_teleport(mobile_serial, destination, source_effect, dest_effect, sound_id)
    local m = mobile.get(mobile_serial)
    if m == nil then
        return
    end

    if source_effect then
        effect.send(m.map_id, m.location_x, m.location_y, m.location_z, TELEPORT_EFFECT_ITEM_ID, 10, 10)
    end

    if not m:teleport(destination.map_id, destination.x, destination.y, destination.z) then
        return
    end

    if dest_effect then
        effect.send(destination.map_id, destination.x, destination.y, destination.z, TELEPORT_EFFECT_ITEM_ID, 10, 10)
    end

    if sound_id > 0 then
        m:play_sound(sound_id)
    end
end

items_teleport = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil or ctx.mobile_id == nil then
            return
        end

        local item_serial = tonumber(ctx.item.serial or 0)
        local mobile_serial = tonumber(ctx.mobile_id or 0)
        if item_serial == nil or item_serial == 0 or mobile_serial == nil or mobile_serial == 0 then
            return
        end

        local item_proxy = item.get(item_serial)
        local mobile_proxy = mobile.get(mobile_serial)
        if item_proxy == nil or mobile_proxy == nil then
            return
        end

        local point_dest = convert.parse_point3d(item_proxy:get_prop("point_dest"))
        if point_dest == nil then
            return
        end

        local destination = {
            map_id = map.to_id(item_proxy:get_prop("map_dest"), mobile_proxy.map_id),
            x = point_dest.x,
            y = point_dest.y,
            z = point_dest.z
        }

        local source_effect = convert.to_bool(item_proxy:get_prop("source_effect"), false)
        local dest_effect = convert.to_bool(item_proxy:get_prop("dest_effect"), false)
        local sound_id = convert.to_int(item_proxy:get_prop("sound_id"), 0)

        local delay_ms = convert.parse_delay_ms(item_proxy:get_prop("delay_ms"), 0)
        if delay_ms <= 0 then
            delay_ms = convert.parse_delay_ms(item_proxy:get_prop("delay"), 0)
        end

        if delay_ms > 0 then
            local timer_name = string.format("item_teleport_%u_%u", item_serial, mobile_serial)
            timer.after(timer_name, delay_ms, function()
                try_teleport(mobile_serial, destination, source_effect, dest_effect, sound_id)
            end)
            return
        end

        try_teleport(mobile_serial, destination, source_effect, dest_effect, sound_id)
    end
}
