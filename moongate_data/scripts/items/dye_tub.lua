items_dye_tub = {
    on_double_click = function(ctx)
        return dye.begin(ctx.session_id, ctx.item.serial, function(target)
            return target ~= nil
        end)
    end
}
