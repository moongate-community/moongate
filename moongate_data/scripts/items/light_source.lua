items_light_source = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = convert.to_int(ctx.item.serial, 0)
        if serial == nil or serial == 0 then
            return
        end

        local light = item.get(serial)
        if light == nil then
            return
        end

        local lit_item_id = convert.to_int(light:get_prop("light_lit_item_id"), 0)
        local unlit_item_id = convert.to_int(light:get_prop("light_unlit_item_id"), 0)
        if lit_item_id <= 0 or unlit_item_id <= 0 then
            return
        end

        local burning = convert.to_bool(light:get_prop("light_burning"), false)
        if burning then
            light:set_item_id(unlit_item_id)
            light:set_prop("light_burning", false)

            local off_sound = convert.to_int(light:get_prop("light_toggle_sound_off"), 0)
            if off_sound > 0 then
                light:play_sound(off_sound)
            end

            return
        end

        light:set_item_id(lit_item_id)
        light:set_prop("light_burning", true)

        local on_sound = convert.to_int(light:get_prop("light_toggle_sound_on"), 0)
        if on_sound > 0 then
            light:play_sound(on_sound)
        end
    end,
}
