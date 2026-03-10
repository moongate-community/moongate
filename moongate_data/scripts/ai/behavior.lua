local M = {
    _definitions = {},
}

function M.register(id, definition)
    if type(id) ~= "string" or id == "" then
        return false
    end

    if type(definition) ~= "table" then
        return false
    end

    M._definitions[id] = definition
    return true
end

function M.get(id)
    if type(id) ~= "string" or id == "" then
        return nil
    end

    return M._definitions[id]
end

function M.all()
    return M._definitions
end

return M
