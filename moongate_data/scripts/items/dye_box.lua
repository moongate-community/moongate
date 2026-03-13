items_dye_box = {
    on_double_click = function(ctx)
        return dye.begin(ctx.session_id, ctx.item.serial, function(target_serial)
            return target_serial ~= nil and target_serial ~= 0
        end)
    end
}
