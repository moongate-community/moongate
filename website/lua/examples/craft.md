## get

A sign that lists the groups of carpentry and how many recipes each holds:

```lua
function carpentry_sign.on_use(serial, user)
    local carpentry = craft.get("carpentry")

    for _, group in ipairs(carpentry.groups) do
        mobile.message(user, group.name .. ": " .. #group.recipes .. " recipes")
    end

    return true
end
```

## resource

Whether a player carries any of the plain boards carpentry takes:

```lua
local function has_wood(user)
    for _, template in ipairs(craft.resource("wood") or {}) do
        if #item.find(user, template) > 0 then
            return true
        end
    end

    return false
end
```
