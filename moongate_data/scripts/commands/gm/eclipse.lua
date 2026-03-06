command.register("eclipse", function(ctx)
    weather.set_global_light(26)
    speech.broadcast("The moon has blocked the sun.")

    if ctx.session_id ~= nil and ctx.session_id > 0 then
        ctx:print("Eclipse started.")
    end
end, {
    description = "Starts a world eclipse and broadcasts a global message.",
    minimum_account_type = "Administrator",
})
