local runtime = dialogue

local M = {}

function M.option(definition)
    definition = definition or {}

    if definition.goto == nil and definition.goto_ ~= nil then
        definition.goto = definition.goto_
    end

    return definition
end

function M.node(definition)
    definition = definition or {}
    definition.options = definition.options or {}
    return definition
end

function M.conversation(id, definition)
    if type(id) ~= "string" or id == "" then
        error("dialogue.conversation id is required", 2)
    end

    if type(definition) ~= "table" then
        error("dialogue.conversation definition table is required", 2)
    end

    definition.id = id
    runtime.register(id, definition)

    return definition
end

return M
