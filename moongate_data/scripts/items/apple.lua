apple = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = tonumber(ctx.item.serial or 0)

        if serial == nil or serial == 0 then
            return
        end

        local apple = item.get(serial)
        if apple == nil then
            return
        end

        local deleted = apple:Delete()

        if ctx.session_id ~= nil then
            if deleted then
                speech.send(ctx.session_id, "You eat the apple.")
            else
                speech.send(ctx.session_id, "You can't eat this apple right now.")
            end
            return
        end

        if deleted then
            speech.broadcast("An apple has been eaten.")
        end
    end,
}
