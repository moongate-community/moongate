-- Moongate prelude. Runs before init.lua on every start.
-- Scripts may only yield through wait(): the scheduler parks the coroutine on the
-- timer wheel and resumes it on the game loop when the time has passed.
function wait(seconds)
    if type(seconds) ~= "number" or seconds < 0 then
        error("wait(seconds) needs a non-negative number", 2)
    end

    return coroutine.yield("wait", seconds)
end
