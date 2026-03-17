local cliloc = require("common.cliloc_ids")

town_banker = {}

function town_banker.get_context_menus(ctx)
    return {
        { key = "open_bank", cliloc_id = cliloc.open_bank },
    }
end

function town_banker.on_selected_context_menu(menu_key, ctx)
    if menu_key ~= "open_bank" then
        return
    end

    local session_id = ctx.session_id
    if session_id == nil or session_id <= 0 then
        return
    end

    bank.open(session_id)
end
