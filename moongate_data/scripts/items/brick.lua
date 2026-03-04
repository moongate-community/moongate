local BRICK_FIRST_GUMP_ID = 0xB10C
local BRICK_SECOND_GUMP_ID = 0xB10D
local BRICK_OPEN_SECOND_BUTTON = 1

gump.on(BRICK_FIRST_GUMP_ID, BRICK_OPEN_SECOND_BUTTON, function(ctx)
    if ctx == nil or ctx.session_id == nil then
        return
    end

    local sender = 0
    if ctx.character_id ~= nil then
        sender = ctx.character_id
    end

    local g = gump.create()
    g:ResizePic(0, 0, 9200, 280, 130)
    g:NoMove()
    g:Text(24, 22, 1152, "Brick - second gump")
    g:Text(24, 52, 0, "You pressed the button.")
    g:Text(24, 72, 0, "This is the follow-up gump.")

    gump.send(ctx.session_id, g, sender, BRICK_SECOND_GUMP_ID, 140, 90)
end)

brick = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.session_id == nil then
            return
        end

        local sender = 0
        if ctx.mobile_id ~= nil then
            sender = ctx.mobile_id
        end

        local g = gump.create()
        g:ResizePic(0, 0, 9200, 300, 160)
        g:NoMove()
        g:Text(24, 18, 1152, "Brick")
        g:Text(24, 48, 0, "First test gump from item double click.")
        g:Text(24, 68, 0, "Press the button to open the second gump.")
        g:Button(24, 108, 4005, 4007, BRICK_OPEN_SECOND_BUTTON)
        g:Text(58, 109, 0, "Open next gump")

        gump.send(ctx.session_id, g, sender, BRICK_FIRST_GUMP_ID, 120, 80)
    end,
}
