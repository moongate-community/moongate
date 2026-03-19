local runtime = scheduled_events

local M = {}

function M.event(id, definition)
    if type(id) ~= "string" or id == "" then
        error("scheduled_events.event id is required", 2)
    end

    if type(definition) ~= "table" then
        error("scheduled_events.event definition table is required", 2)
    end

    definition.id = id
    runtime.register(id, definition)

    return definition
end

return M
