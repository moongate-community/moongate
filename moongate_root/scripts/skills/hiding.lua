-- ==============================================================================
-- Moongate - scripts/skills/hiding.lua
--
-- What it is for:
--   The skill script of Hiding, as in ModernUO without what needs a fight or a
--   house: a player who uses the skill is tried at it, from 0 points, where
--   it may just succeed, to 100, where it never fails. On a success it is
--   hidden, out of war mode; on a failure it is seen, also when it was hidden.
--   Either way it waits before another skill the delay of hiding in
--   data/skills.toml, 10 seconds. Its first step shows it again (the server
--   does that): there is no Stealth yet.
--
--   A skill script is a table named after the skill, as data/skills.toml
--   names it, in scripts/skills/<skill>.lua.
--
-- Functions:
--   on_use(user)   the player user uses the skill. It may return the seconds to
--                  wait before another skill, in place of the delay of
--                  data/skills.toml; this one returns none
-- ==============================================================================

hiding = {}

-- The client's own texts.
local HIDDEN = 501240   -- You have hidden yourself well.
local FAILED = 501241   -- You can't seem to hide here.

-- Called when a player uses the skill.
function hiding.on_use(user)
    if skill.check(user, "hiding", 0, 100) then
        mobile.set_hidden(user, true)
        mobile.set_war_mode(user, false)
        mobile.message_cliloc(user, HIDDEN)
    else
        mobile.set_hidden(user, false)
        mobile.message_cliloc(user, FAILED)
    end
end
