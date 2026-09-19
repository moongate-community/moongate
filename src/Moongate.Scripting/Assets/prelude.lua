-- Moongate prelude. Runs before init.lua on every start.
-- print goes to the server log. The engine installs a host function that takes
-- one line; this wrapper gives it the standard print semantics: every argument
-- through tostring (so __tostring is honoured), joined with tabs.
local write_line = print
function print(...)
    local parts = {}
    for i = 1, select("#", ...) do
        parts[i] = tostring((select(i, ...)))
    end

    write_line(table.concat(parts, "\t"))
end

-- Scripts may only yield through wait(): the scheduler parks the coroutine on the
-- timer wheel and resumes it on the game loop when the time has passed.
function wait(seconds)
    if type(seconds) ~= "number" or seconds <= 0 then
        error("wait(seconds) needs a positive number of seconds", 2)
    end

    return coroutine.yield("wait", seconds)
end
