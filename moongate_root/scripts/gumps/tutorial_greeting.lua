-- ==============================================================================
-- Moongate - scripts/gumps/tutorial_greeting.lua
--
-- What it is for:
--   The script of step 2 of the gump tutorial (docs/tutorials/first-gump.md).
--   The table is named after the gump id; its functions are the on_click
--   names of templates/gumps/tutorial_greeting.xml, plus on_close.
--
-- Functions:
--   done(player, response, args)  the Done button; args.name is the name
--                                 step 1 bound
--   on_close(player, args, reason) the gump went away without a button
-- ==============================================================================

tutorial_greeting = {}

function tutorial_greeting.done(player, response, args)
    log.info("Player {Player} chose the name {Name}", player, args.name)
end

function tutorial_greeting.on_close(player, args, reason)
    log.info("The greeting was closed: {Reason}", reason)
end
