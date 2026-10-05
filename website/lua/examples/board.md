## open

The item script of a bulletin board: a double click opens it on the player.

```lua
function bulletin_board.on_use(serial, user)
    board.open(serial, user)

    return true
end
```

## post

A town crier leaves a notice on the board beside it, then answers under it:

```lua
local notice = board.post(notice_board, "The town crier", "Hear ye", {
    "The bank of Britain is closed today.",
    "",
    "Come back tomorrow."
})

if notice then
    board.post(notice_board, "The town crier", "Re: Hear ye", { "It is open again." }, notice)
end
```

## messages

What is on a board, thread by thread:

```lua
for _, message in ipairs(board.messages(notice_board)) do
    local kind = message.thread and "  reply" or "thread"

    log.info(kind .. " by " .. message.name .. ": " .. message.subject .. " (" .. #message.lines .. " lines)")
end
```

## remove

A staff script clears what a script posted more than a day ago:

```lua
for _, message in ipairs(board.messages(notice_board)) do
    if not message.poster and world.now() - message.posted_at > 86400 then
        board.remove(message.serial)
    end
end
```
