## give

```lua
-- The shipped welcome letter declares contact_name.
local letter = book.give(player, "welcome_letter", { contact_name = "Vega" })
if letter then
    book.open(letter, player)
end
```

The recipient name and all values are saved when the scroll is created.
A second reader sees the same text. Nil means no item was created.

## write

```lua
local written = book.write(scroll, "welcome_letter", player, {
    contact_name = "Orione"
})
```

The item must use a nonstackable readable template and must not be held.
False leaves its previous name, title, author and body unchanged.

## open

```lua
-- In an item script:
function readable_scroll.on_use(serial, user)
    book.open(serial, user)
    return true
end
```

This displays saved text, with normal item access and range checks. From Lua,
true means the open was queued for the next loop turn; an item moved or deleted,
or a replaced/disconnected session, prevents delivery. False means refused.
