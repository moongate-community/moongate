items_bulletin_board = {
    on_double_click = function(ctx)
        return bulletin.open(ctx.session_id, ctx.item.serial)
    end
}
