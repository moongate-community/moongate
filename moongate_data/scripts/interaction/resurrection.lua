local resurrection_gump = require("gumps.resurrection")

function on_resurrection_offer(session_id, character_id, source_type)
    if session_id == nil or character_id == nil then
        return false
    end

    return resurrection_gump.open(session_id, character_id, source_type)
end
