-- Moongate prelude: loaded before init.lua.
function wait(seconds)
    return coroutine.yield("wait", seconds)
end
