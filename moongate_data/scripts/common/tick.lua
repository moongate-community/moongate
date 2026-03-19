local tick = {}

function tick.state(intervals, start_ms)
    local state = {
        intervals = {},
        last_run_ms = {},
    }

    local initial_ms = start_ms or 0

    for key, interval_ms in pairs(intervals or {}) do
        state.intervals[key] = interval_ms
        state.last_run_ms[key] = initial_ms
    end

    return state
end

function tick.ready(state, key, now_ms)
    if state == nil or key == nil or now_ms == nil then
        return false
    end

    local interval_ms = state.intervals[key]
    if interval_ms == nil then
        return false
    end

    local last_run_ms = state.last_run_ms[key] or 0
    return now_ms - last_run_ms >= interval_ms
end

function tick.run(state, key, now_ms, action)
    if not tick.ready(state, key, now_ms) then
        return false
    end

    state.last_run_ms[key] = now_ms

    if action ~= nil then
        action()
    end

    return true
end

function tick.reset(state, key, now_ms, interval_ms)
    if state == nil or key == nil then
        return false
    end

    if interval_ms ~= nil then
        state.intervals[key] = interval_ms
    end

    state.last_run_ms[key] = now_ms or 0
    return true
end

return tick
