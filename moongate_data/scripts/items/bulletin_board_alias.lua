items_bb = {
    on_double_click = function(ctx)
        return bulletin.open(ctx.session_id, ctx.item.serial)
    end
}
