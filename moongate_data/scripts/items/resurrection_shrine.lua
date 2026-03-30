items_resurrection_shrine = {
    on_double_click = function(ctx)
        if
            ctx == nil or
            ctx.session_id == nil or
            ctx.mobile_id == nil or
            ctx.item == nil or
            ctx.item.serial == nil
        then
            return false
        end

        return resurrection.offer_ankh(ctx.session_id, ctx.mobile_id, ctx.item.serial)
    end,
}
