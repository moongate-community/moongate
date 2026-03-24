items_clock = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.session_id == nil or ctx.mobile_id == nil then
            return
        end

        local general = clock.describe(ctx.mobile_id)
        if general ~= nil and general ~= "" then
            speech.send(ctx.session_id, general)
        end

        local exact = clock.exact_time(ctx.mobile_id)
        if exact ~= nil and exact ~= "" then
            speech.send(ctx.session_id, exact .. " to be exact.")
        end
    end,
}
