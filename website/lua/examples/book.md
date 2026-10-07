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

This displays saved text, with normal item access and range checks. Native books submit
their cover and page packets during the call; true means both packets were accepted for
sending. Scrolls opened from Lua are queued for the next loop turn, so true means the request
was queued. Before opening a queued scroll, the server rechecks the reader and item identities,
the original session and item access. Deletion, replacement, disconnection or loss of access
prevents delivery; moving a scroll only prevents delivery if it becomes inaccessible.
False means the request was refused.
