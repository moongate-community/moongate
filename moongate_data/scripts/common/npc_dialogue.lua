local npc_dialogue = {}

local function normalize_config(config)
    if type(config) ~= "table" then
        return {
            conversation_id = nil,
            prompt_file = nil,
        }
    end

    return {
        conversation_id = config.conversation_id,
        prompt_file = config.prompt_file,
    }
end

function npc_dialogue.init(npc, config)
    if npc == nil then
        return false
    end

    local normalized = normalize_config(config)
    local initialized = false

    if type(normalized.conversation_id) == "string" and normalized.conversation_id ~= "" then
        initialized = dialogue.init(npc, normalized.conversation_id) or initialized
    end

    if type(normalized.prompt_file) == "string" and normalized.prompt_file ~= "" then
        initialized = ai_dialogue.init(npc, normalized.prompt_file) or initialized
    end

    return initialized
end

function npc_dialogue.listener(npc, speaker, text, config)
    if npc == nil or speaker == nil or type(text) ~= "string" or text == "" then
        return false
    end

    local normalized = normalize_config(config)

    if type(normalized.conversation_id) == "string" and normalized.conversation_id ~= "" then
        if dialogue.listener(npc, speaker, text) then
            return true
        end
    end

    if type(normalized.prompt_file) == "string" and normalized.prompt_file ~= "" then
        if ai_dialogue.listener(npc, speaker, text) then
            return true
        end
    end

    return false
end

function npc_dialogue.idle(npc, config)
    if npc == nil then
        return false
    end

    local normalized = normalize_config(config)

    if type(normalized.prompt_file) == "string" and normalized.prompt_file ~= "" then
        return ai_dialogue.idle(npc)
    end

    return false
end

return npc_dialogue
