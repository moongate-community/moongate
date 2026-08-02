-- Example gump script. An item template with `ScriptId: items.example_signpost` runs these hooks —
-- the dot is a namespace, so that id is this file, scripts/items/example_signpost.lua.
--
-- Double-clicking the item opens a dialog and acts on what the player chose. That is the whole
-- loop: draw, wait, answer, act.

local signpost = {
    id = "items.example_signpost",
}

function signpost.on_double_click(ctx)
-- Always check the actor: a hook can fire with nobody behind it, and there is no one to
-- show a gump to.
    if ctx.actor == nil then
        return
    end

    gump.show(ctx.actor.serial, "example_signpost", function(g)
        g.background { x = 0, y = 0, w = 320, h = 200, art = 5054 }
        g.label { x = 24, y = 20, hue = 1153, text = "Where would you like to go?" }

        -- One button per destination. The id is what comes back in the response, so it is the
        -- only number here you have to keep track of.
        g.button { x = 24, y = 60, art = 4005, pressed = 4007, id = 1 }
        g.label { x = 60, y = 60, hue = 0, text = "Britain" }

        g.button { x = 24, y = 90, art = 4005, pressed = 4007, id = 2 }
        g.label { x = 60, y = 90, hue = 0, text = "Trinsic" }

        -- A checkbox rides back in `switches` rather than in `button`.
        g.check { x = 24, y = 130, art = 210, pressed = 211, id = 10 }
        g.label { x = 60, y = 130, hue = 0, text = "Announce my arrival" }
    end,
        function(r)
        -- Button 0 is the client's own close button: the player changed their mind.
            if r.button == 0 then
                return
            end

            local destination = r.button == 1 and { name = "Britain", x = 1495, y = 1629 }
            or  { name = "Trinsic", x = 1828, y = 2745 }

            local announce = false

            for _, id in ipairs(r.switches) do
                if id == 10 then
                    announce = true
                end
            end

            local traveller = mobile.ref(ctx.actor.serial)

            -- The mobile may be gone by the time the player answers: ref returns nil rather than
            -- letting you act on a stale target.
            if traveller == nil then
                return
            end

            traveller.teleport(destination.x, destination.y, 0)

            if announce then
                traveller.say("I have arrived in " .. destination.name .. "!")
            end
        end)
end

return signpost
