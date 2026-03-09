items_spawn = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = convert.to_int(ctx.item.serial, 0)
        if serial <= 0 then
            return
        end

        local ok = spawn.activate(serial)

        if ctx.session_id ~= nil and ctx.session_id > 0 then
            if ok then
                speech.send(ctx.session_id, "Spawner activated.")
            else
                speech.send(ctx.session_id, "Spawner activation failed.")
            end
        end
    end
}
